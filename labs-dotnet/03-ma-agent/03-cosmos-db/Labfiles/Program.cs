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

Console.WriteLine("MA Agent - Manager Assistant");
Console.WriteLine("========================================");
Console.WriteLine("Type 'quit' to exit\n");

// Define MA Agent instructions
string maAgentInstructions = """
    You are "MA Agent" — an AI assistant that helps managers review and explore Payment Voucher (PV) requests.

    YOUR ROLE:
    - Help managers understand and analyze PV request details shared in the conversation
    - Summarize PV data clearly and concisely
    - Identify missing fields, potential issues, or anomalies in PV data
    - Answer questions about PV content based on the data provided
    - Highlight important information such as expense amounts, requestors, and approval status
    - When the manager asks to see PV requests, call the GetPvRequests tool with the appropriate approval status filter
    - When the manager asks to approve or set a PV back to pending, call the UpdatePvApprovalStatus tool with the PV id and the new status

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

// Helper — returns the Cosmos DB container client
Microsoft.Azure.Cosmos.Container GetCosmosContainer()
{
    var connectionString = configuration["CosmosDB:ConnectionString"]
        ?? throw new InvalidOperationException("Set CosmosDB:ConnectionString in appsettings.json");
    var databaseName = configuration["CosmosDB:DatabaseName"] ?? "pv-agent-db";
    var containerName = configuration["CosmosDB:ContainerName"] ?? "payment-vouchers";

    var cosmosClient = new CosmosClient(connectionString);
    return cosmosClient.GetDatabase(databaseName).GetContainer(containerName);
}

// GetPvRequests tool — queries Cosmos DB for PV documents filtered by approval status
// [TODO] Part 1: Implement this method to query Azure Cosmos DB
//        - Validate that approvalStatus is "Pending" or "Approved"; return an error message if not
//        - Use GetCosmosContainer() to get the container client
//        - Use GetItemQueryStreamIterator with "SELECT * FROM c" to fetch all documents
//        - Parse each response page with JsonNode.ParseAsync, read the "Documents" array
//        - Filter documents where approval.status matches approvalStatus
//        - Return the matching documents as an indented JSON string, or a "not found" message
[Description("Retrieve PV requests filtered by their approval status. Call this when the manager asks to see pending or approved PV requests.")]
async Task<string> GetPvRequests(
    [Description("The approval status to filter by. Must be exactly 'Pending' or 'Approved'.")] string approvalStatus,
    CancellationToken ct = default)
{
    if (approvalStatus != "Pending" && approvalStatus != "Approved")
        return $"Invalid approval status '{approvalStatus}'. Must be 'Pending' or 'Approved'.";

    // [TODO] Replace with Cosmos DB query implementation
    return $"No PV requests found with approval status '{approvalStatus}'. (Connect to Cosmos DB to retrieve real data)";
}

// UpdatePvApprovalStatus tool — reads a PV document, updates approval.status, and replaces it
// [TODO] Part 2: Implement this method to update a PV document in Azure Cosmos DB
//        - Validate that newStatus is "Pending" or "Approved"; return an error message if not
//        - Use GetCosmosContainer() to get the container client
//        - Use ReadItemStreamAsync(pvId, new PartitionKey(pvId)) to read the document as a stream
//        - Parse the stream with JsonNode.ParseAsync, cast to JsonObject
//        - Navigate to document["approval"] and update ["status"] = newStatus
//        - Serialize the modified document back to a UTF-8 MemoryStream
//        - Use ReplaceItemStreamAsync(stream, pvId, new PartitionKey(pvId)) to persist the change
//        - Return a success or error message
[Description("Update the approval status of a specific PV request by its id. Call this when the manager confirms an approval or status change.")]
async Task<string> UpdatePvApprovalStatus(
    [Description("The unique id of the PV request to update")] string pvId,
    [Description("The new approval status. Must be exactly 'Pending' or 'Approved'.")] string newStatus,
    CancellationToken ct = default)
{
    if (newStatus != "Pending" && newStatus != "Approved")
        return $"Invalid status '{newStatus}'. Must be 'Pending' or 'Approved'.";

    // [TODO] Replace with Cosmos DB read + update + replace implementation
    return $"PV with id '{pvId}' not found. (Connect to Cosmos DB to update real data)";
}

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
