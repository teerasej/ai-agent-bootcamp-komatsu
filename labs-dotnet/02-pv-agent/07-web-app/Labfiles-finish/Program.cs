using Microsoft.Agents.AI;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var endpoint = configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("Set AzureOpenAI:Endpoint in appsettings.json");
var deploymentName = configuration["AzureOpenAI:DeploymentName"]
    ?? throw new InvalidOperationException("Set AzureOpenAI:DeploymentName in appsettings.json");
var apiKey = configuration["AzureOpenAI:ApiKey"]
    ?? throw new InvalidOperationException("Set AzureOpenAI:ApiKey in appsettings.json");

// --- PV Agent instructions ---
string pvAgentInstructions = """
    You are "PV Agent" — an AI assistant that helps requestors draft a Payment Voucher (PV) request.

    YOUR ROLE:
    - Guide the user through collecting all required PV fields via natural conversation
    - Ask only for missing critical fields, one at a time
    - State your assumptions explicitly (for example, "I'll use THB as the default currency")
    - Show a confirmation summary before marking the PV as ReadyForSubmission
    - Always output a valid JSON object as the final step
    - When the user provides a project name, call the GetProjectBudget tool to look up the project's budget and remaining budget. Use this data to populate project.budgetSummary in the output JSON.
    - when user request to submit the pv, submit PV data to system.

    REQUIRED FIELDS (must be collected before ReadyForSubmission):
    - pvTitle: Short, descriptive title for the voucher
    - requestDate: Date in YYYY-MM-DD format
    - requestor.name: Full name of the person making the request
    - payee.name: Full name or company of who will receive the payment
    - purpose.for: What specifically is being paid for
    - purpose.objective: The business reason or objective
    - expense.type: MUST be exactly "MonthlyFee" or "OneTime" — map user's words to one of these two values
    - expense.amount.value: A positive number — if user says text like "one thousand", convert to 1000
    - expense.amount.currency: Default to "THB" unless user specifies otherwise
    - expense.budgetType: MUST be exactly "Expense" or "Investment" — derive this from context:
        - Use "Expense" for recurring or operational costs (subscriptions, monthly fees, training, consumables)
        - Use "Investment" for one-time capital expenditures that create lasting value (equipment, tool licenses, infrastructure)
        - If the user's intent is ambiguous, infer from expense.type: MonthlyFee → lean toward "Expense", OneTime → consider both and ask if still unclear
    - project.projectName: Name of the project this payment belongs to — never invent, always ask user
    - approval.approverName: Name of the approver

    CONSTRAINTS:
    - You do NOT approve requests or change budget limits
    - You do NOT invent project names, payee names, or amounts
    - approval.status is always "Pending" for new requests
   
    OUTPUT FORMAT (produce this JSON when all fields are collected and confirmed):
    {
      "pv": {
        "pvTitle": "...",
        "requestDate": "YYYY-MM-DD",
        "requestor": { "name": "..." },
        "payee": { "name": "..." },
        "purpose": {
          "for": "...",
          "objective": "..."
        },
        "expense": {
          "type": "MonthlyFee | OneTime",
          "budgetType": "Expense | Investment",
          "amount": { "value": 0, "currency": "THB" }
        },
        "project": {
          "projectName": "...",
          "budgetSummary": {
            "totalBudget": 0,
            "remainingBudget": 0
          }
        },
        "approval": { "approverName": "...", "status": "Pending" },
        "status": "Draft | ReadyForSubmission"
      }
    }
    """;

// --- Tools ---

[Description("Look up the total budget and remaining budget for a given project from the CSV data file.")]
static string GetProjectBudget(
    [Description("The name of the project to look up budget information for")] string projectName)
{
    string dataPath = Path.Combine(AppContext.BaseDirectory, "data", "projects_budget.csv");
    if (!File.Exists(dataPath))
        return "Budget data file not found. Please ensure the data file exists.";

    string[] lines = File.ReadAllLines(dataPath);
    if (lines.Length < 2)
        return "Budget data file is empty or has no records.";

    string[] headers = lines[0].Split(',');
    int nameIdx   = Array.FindIndex(headers, h => h.Trim().Equals("project name",   StringComparison.OrdinalIgnoreCase));
    int budgetIdx = Array.FindIndex(headers, h => h.Trim().Equals("budget",         StringComparison.OrdinalIgnoreCase));
    int remainIdx = Array.FindIndex(headers, h => h.Trim().Equals("remain budget",  StringComparison.OrdinalIgnoreCase));

    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i])) continue;
        string[] fields = lines[i].Split(',');
        if (nameIdx < fields.Length && fields[nameIdx].Trim().Equals(projectName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            string budget    = budgetIdx < fields.Length ? fields[budgetIdx].Trim() : "N/A";
            string remaining = remainIdx < fields.Length ? fields[remainIdx].Trim() : "N/A";
            return $"{{\"projectName\": \"{fields[nameIdx].Trim()}\", \"totalBudget\": {budget}, \"remainingBudget\": {remaining}}}";
        }
    }

    return $"Project '{projectName}' not found in the budget data.";
}

[Description("submit pv data to system for approval")]
async Task<string> SubmitPv(
    [Description("The complete PV JSON object as a string")]
    string pvJson, CancellationToken ct = default)
{
    var connectionString = configuration["CosmosDB:ConnectionString"]
        ?? throw new InvalidOperationException("Set CosmosDB:ConnectionString in appsettings.json");
    var databaseName  = configuration["CosmosDB:DatabaseName"]  ?? "pv-agent-db";
    var containerName = configuration["CosmosDB:ContainerName"] ?? "payment-vouchers";

    try
    {
        var root     = JsonNode.Parse(pvJson)!;
        var document = (root["pv"] as JsonObject) ?? (JsonObject)root!;
        string newId = Guid.NewGuid().ToString();
        document["id"] = newId;

        using var cosmosClient = new CosmosClient(connectionString);
        var container = cosmosClient.GetDatabase(databaseName).GetContainer(containerName);

        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString());
        using var stream = new MemoryStream(bytes);
        await container.CreateItemStreamAsync(stream, new PartitionKey(newId));

        return $"PV submitted successfully and stored in Azure Cosmos DB with id: {newId}";
    }
    catch (Exception ex)
    {
        return $"PV submission failed: {ex.Message}";
    }
}

// --- Create the agent with both tools registered ---
AIAgent agent = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsAIAgent(
        instructions: pvAgentInstructions,
        name: "PVAgent",
        tools: [
            AIFunctionFactory.Create(GetProjectBudget),
            AIFunctionFactory.Create(SubmitPv)
        ]);

// --- Session store: map browser sessionId → AgentSession (conversation history) ---
var sessions = new ConcurrentDictionary<string, AgentSession>();

var app = builder.Build();
app.UseStaticFiles();

// POST /chat
// Receives { "message": "...", "sessionId": "..." } and streams the agent response
// back to the browser as Server-Sent Events (SSE).
app.MapPost("/chat", async (HttpContext ctx) =>
{
    // Read the JSON body
    using var reader  = new StreamReader(ctx.Request.Body);
    var bodyText      = await reader.ReadToEndAsync();
    using var doc     = JsonDocument.Parse(bodyText);
    var message       = doc.RootElement.GetProperty("message").GetString()   ?? "";
    var sessionId     = doc.RootElement.GetProperty("sessionId").GetString() ?? Guid.NewGuid().ToString();

    // Look up an existing session or create a new one for this browser tab
    if (!sessions.TryGetValue(sessionId, out var session))
    {
        session = await agent.CreateSessionAsync();
        sessions[sessionId] = session;
    }

    // Configure the response as an SSE stream
    ctx.Response.ContentType              = "text/event-stream";
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";

    // Stream each agent token as an SSE data event so the browser can render it incrementally
    await foreach (var update in agent.RunStreamingAsync(message, session))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            var data = JsonSerializer.Serialize(update.Text);
            await ctx.Response.WriteAsync($"data: {data}\n\n");
            await ctx.Response.Body.FlushAsync();
        }
    }

    // Signal end of stream
    await ctx.Response.WriteAsync("data: [DONE]\n\n");
    await ctx.Response.Body.FlushAsync();
});

app.MapFallbackToFile("index.html");
app.Run();
