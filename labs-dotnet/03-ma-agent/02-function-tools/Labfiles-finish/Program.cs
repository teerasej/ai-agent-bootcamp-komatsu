using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;
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

Console.WriteLine("MA Agent - Manager Assistant");
Console.WriteLine("========================================");
Console.WriteLine("Type 'quit' to exit\n");

// Define MA Agent instructions (with tool-calling rules added)
string maAgentInstructions = """
    You are "MA Agent" — an AI assistant that helps managers review and explore Payment Voucher (PV) requests.

    YOUR ROLE:
    - Help managers understand and analyze PV request details shared in the conversation
    - Summarize PV data clearly and concisely
    - Identify missing fields, potential issues, or anomalies in PV data
    - Answer questions about PV content based on the data provided
    - Highlight important information such as expense amounts, requestors, and approval status
    - Compare multiple PV entries when asked
    - When the manager asks to see PV requests, ALWAYS call the GetPvRequests tool with the appropriate approval status filter
    - When the manager asks to approve a PV or set it back to pending, call the UpdatePvApprovalStatus tool with the exact PV id and the new status

    UNDERSTANDING PV DATA:
    The PV data follows this JSON structure:
    {
      "id": "unique-id",
      "pvTitle": "Short descriptive title",
      "requestDate": "YYYY-MM-DD",
      "requestor": { "name": "Name of the requestor" },
      "payee": { "name": "Name or company receiving payment" },
      "purpose": {
        "for": "What is being paid for",
        "objective": "Business reason or objective"
      },
      "expense": {
        "type": "MonthlyFee | OneTime",
        "budgetType": "Expense | Investment",
        "amount": { "value": 0, "currency": "THB" }
      },
      "project": {
        "projectName": "Project name",
        "budgetSummary": {
          "totalBudget": 0,
          "remainingBudget": 0
        }
      },
      "approval": { "approverName": "Name of approver", "status": "Pending | Approved" },
      "status": "Draft | ReadyForSubmission"
    }

    KEY FIELD INTERPRETATIONS:
    - expense.type "MonthlyFee": Recurring monthly payment
    - expense.type "OneTime": Single one-time payment
    - approval.status "Pending": Awaiting manager approval
    - approval.status "Approved": Already approved
    - status "Draft": Incomplete or not yet submitted
    - status "ReadyForSubmission": Complete and ready for review

    CONSTRAINTS:
    - You do NOT modify PV data directly — always use the available tools
    - You do NOT create new PV requests
    - If PV data is incomplete, note which fields are missing but do not fill them in
    - Always base your analysis on the exact data provided — do not invent or assume unreported values
    - When updating approval status, you MUST use the exact PV id from the data returned by GetPvRequests
    """;

// Sample PV data — placeholder for Cosmos DB integration in the next exercise.
// Stored as a mutable List<string> where each element is a raw JSON string (one PV document).
// Storing as strings (rather than deserialized objects) allows in-place status updates
// without needing a defined C# type for the full PV schema.
List<string> samplePvData =
[
    """{"id":"pv-001","pvTitle":"Monthly GitHub Copilot Subscription","requestDate":"2026-03-01","requestor":{"name":"Somchai Jaidee"},"payee":{"name":"GitHub Inc."},"purpose":{"for":"Monthly developer tool subscription","objective":"Improve developer productivity for IT Internal project"},"expense":{"type":"MonthlyFee","budgetType":"Expense","amount":{"value":5000,"currency":"THB"}},"project":{"projectName":"IT Internal","budgetSummary":{"totalBudget":50000,"remainingBudget":45000}},"approval":{"approverName":"Napat Ruangroj","status":"Pending"},"status":"ReadyForSubmission"}""",
    """{"id":"pv-002","pvTitle":"Conference Registration Fee","requestDate":"2026-03-15","requestor":{"name":"Prasert Kaewkla"},"payee":{"name":"TechConf Thailand"},"purpose":{"for":"Annual developer conference registration","objective":"Team learning and networking"},"expense":{"type":"OneTime","budgetType":"Investment","amount":{"value":3500,"currency":"THB"}},"project":{"projectName":"HR Development","budgetSummary":{"totalBudget":20000,"remainingBudget":16500}},"approval":{"approverName":"Manee Srisuk","status":"Approved"},"status":"ReadyForSubmission"}""",
    """{"id":"pv-003","pvTitle":"Ergonomic Office Chairs","requestDate":"2026-03-20","requestor":{"name":"Wanchai Teeraphon"},"payee":{"name":"Office Furniture Co., Ltd."},"purpose":{"for":"Purchase of 5 ergonomic chairs","objective":"Improve workspace comfort for HR team"},"expense":{"type":"OneTime","budgetType":"Investment","amount":{"value":25000,"currency":"THB"}},"project":{"projectName":"HR Internal","budgetSummary":{"totalBudget":100000,"remainingBudget":75000}},"approval":{"approverName":"Manee Srisuk","status":"Approved"},"status":"ReadyForSubmission"}"""
];

// Create the AIAgent with both function tools registered
AIAgent agent = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: maAgentInstructions,
        name: "MAAgent",
        tools: [
            AIFunctionFactory.Create(GetPvRequests),
            AIFunctionFactory.Create(UpdatePvApprovalStatus)
        ]);

// Create an AgentSession to maintain conversation history across turns
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine("MA Agent is ready. Ask to see PV requests or type 'quit' to exit.\n");

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

// ─── Tool Functions ──────────────────────────────────────────────────────────

// GetPvRequests — filters samplePvData by approval status and returns a JSON array
[Description("Retrieve PV requests filtered by their approval status. Call this when the manager asks to see, list, or review PV requests by status. Returns a JSON array of matching PV documents.")]
string GetPvRequests(
    [Description("The approval status to filter by. Must be exactly 'Pending' or 'Approved'.")] string approvalStatus)
{
    if (approvalStatus != "Pending" && approvalStatus != "Approved")
        return $"Invalid approval status '{approvalStatus}'. Must be 'Pending' or 'Approved'.";

    // Parse each raw JSON string, filter by approval.status, collect matches
    var filtered = samplePvData
        .Select(s => JsonNode.Parse(s)!)
        .Where(n => n["approval"]?["status"]?.GetValue<string>() == approvalStatus)
        .ToList();

    if (filtered.Count == 0)
        return $"No PV requests found with approval status '{approvalStatus}'.";

    Console.WriteLine($"\n[In-memory] Found {filtered.Count} PV(s) with status '{approvalStatus}'\n");

    return JsonSerializer.Serialize(filtered, new JsonSerializerOptions { WriteIndented = true });
}

// UpdatePvApprovalStatus — finds a PV by id, updates approval.status, saves back to samplePvData
[Description("Update the approval status of a specific PV request by its id. Call this when the manager confirms they want to approve a PV or revert it to Pending.")]
string UpdatePvApprovalStatus(
    [Description("The unique id of the PV request to update (e.g. 'pv-001'). Must match the id from GetPvRequests results.")] string pvId,
    [Description("The new approval status. Must be exactly 'Pending' or 'Approved'.")] string newStatus)
{
    if (newStatus != "Pending" && newStatus != "Approved")
        return $"Invalid status '{newStatus}'. Must be 'Pending' or 'Approved'.";

    for (int i = 0; i < samplePvData.Count; i++)
    {
        var node = JsonNode.Parse(samplePvData[i])!;
        if (node["id"]?.GetValue<string>() != pvId) continue;

        string oldStatus = node["approval"]!["status"]!.GetValue<string>();
        string pvTitle = node["pvTitle"]?.GetValue<string>() ?? pvId;

        // Mutate the node and write back as a JSON string
        node["approval"]!["status"] = newStatus;
        samplePvData[i] = node.ToJsonString();

        Console.WriteLine($"\n[Update] PV '{pvId}' approval status changed: {oldStatus} → {newStatus}\n");
        return $"PV '{pvId}' ({pvTitle}) approval status updated from '{oldStatus}' to '{newStatus}' successfully.";
    }

    return $"PV with id '{pvId}' not found.";
}
