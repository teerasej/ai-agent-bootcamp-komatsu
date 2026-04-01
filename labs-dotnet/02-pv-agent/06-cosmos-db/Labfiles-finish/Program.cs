using Microsoft.Agents.AI;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using System.Text.Json.Nodes;

// Load configuration from appsettings.json
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var endpoint = configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("Set AzureOpenAI:Endpoint in appsettings.json");
var deploymentName = configuration["AzureOpenAI:DeploymentName"]
    ?? throw new InvalidOperationException("Set AzureOpenAI:DeploymentName in appsettings.json");
var apiKey = configuration["AzureOpenAI:ApiKey"]
    ?? throw new InvalidOperationException("Set AzureOpenAI:ApiKey in appsettings.json");

// Clear the console
Console.Clear();

Console.WriteLine("PV Agent - Payment Voucher Assistant");
Console.WriteLine("=====================================");
Console.WriteLine("Type 'quit' to exit\n");

// Define PV Agent instructions
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

// GetProjectBudget tool — looks up budget data from the CSV file
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
    int nameIdx = Array.FindIndex(headers, h => h.Trim().Equals("project name", StringComparison.OrdinalIgnoreCase));
    int budgetIdx = Array.FindIndex(headers, h => h.Trim().Equals("budget", StringComparison.OrdinalIgnoreCase));
    int remainIdx = Array.FindIndex(headers, h => h.Trim().Equals("remain budget", StringComparison.OrdinalIgnoreCase));

    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i])) continue;
        string[] fields = lines[i].Split(',');
        if (nameIdx < fields.Length && fields[nameIdx].Trim().Equals(projectName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            string budget = budgetIdx < fields.Length ? fields[budgetIdx].Trim() : "N/A";
            string remaining = remainIdx < fields.Length ? fields[remainIdx].Trim() : "N/A";
            return $"{{\"projectName\": \"{fields[nameIdx].Trim()}\", \"totalBudget\": {budget}, \"remainingBudget\": {remaining}}}";
        }
    }

    return $"Project '{projectName}' not found in the budget data.";
}

// SubmitPv tool — inserts the completed PV document into Azure Cosmos DB
[Description("submit pv data to system for approval")]
async Task<string> SubmitPv(
    [Description("The complete PV JSON object as a string")]
    string pvJson, CancellationToken ct = default)
{
    var connectionString = configuration["CosmosDB:ConnectionString"]
        ?? throw new InvalidOperationException("Set CosmosDB:ConnectionString in appsettings.json");
    var databaseName = configuration["CosmosDB:DatabaseName"] ?? "pv-agent-db";
    var containerName = configuration["CosmosDB:ContainerName"] ?? "payment-vouchers";

    try
    {
        // Parse the JSON string from the agent
        var root = JsonNode.Parse(pvJson)!;

        // Extract the inner "pv" object if the agent wrapped it; otherwise use the root
        var document = (root["pv"] as JsonObject) ?? (JsonObject)root!;

        // Cosmos DB requires a unique "id" field at the document root
        string newId = Guid.NewGuid().ToString();
        document["id"] = newId;

        // Connect to Cosmos DB using the primary connection string
        using var cosmosClient = new CosmosClient(connectionString);
        var container = cosmosClient.GetDatabase(databaseName).GetContainer(containerName);

        // Serialize the document and insert via stream API (avoids SDK type-system conflicts)
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString());
        using var stream = new MemoryStream(bytes);
        await container.CreateItemStreamAsync(stream, new PartitionKey(newId));

        Console.WriteLine($"\n[Cosmos DB] Document inserted with id: {newId}\n");
        return $"PV submitted successfully and stored in Azure Cosmos DB with id: {newId}";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[Cosmos DB] Insertion failed: {ex.Message}\n");
        return $"PV submission failed: {ex.Message}";
    }
}

// Create the AIAgent with both tool functions registered
AIAgent agent = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: pvAgentInstructions,
        name: "PVAgent",
        tools: [
            AIFunctionFactory.Create(GetProjectBudget),
            AIFunctionFactory.Create(SubmitPv)
        ]);

// Create an AgentSession to maintain conversation history across turns
AgentSession session = await agent.CreateSessionAsync();

// Conversation loop — read user input and stream agent responses
while (true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(userInput)) continue;
    if (userInput.ToLower() == "quit") break;

    Console.Write("\nAgent: ");

    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(userInput, session))
    {
        Console.Write(update.Text);
    }

    Console.WriteLine("\n");
}
