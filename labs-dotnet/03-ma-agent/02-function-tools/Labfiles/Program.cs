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

// Define MA Agent instructions
// [TODO 4] After implementing the tools, add the following two lines at the END of YOUR ROLE:
//   - When the manager asks to see PV requests, ALWAYS call the GetPvRequests tool with the appropriate approval status filter
//   - When the manager asks to approve a PV or set it back to pending, call the UpdatePvApprovalStatus tool with the exact PV id and the new status
// Also add the following line at the end of CONSTRAINTS:
//   - When updating approval status, you MUST use the exact PV id from the data returned by GetPvRequests
string maAgentInstructions = """
    You are "MA Agent" — an AI assistant that helps managers review and explore Payment Voucher (PV) requests.

    YOUR ROLE:
    - Help managers understand and analyze PV request details shared in the conversation
    - Summarize PV data clearly and concisely
    - Identify missing fields, potential issues, or anomalies in PV data
    - Answer questions about PV content based on the data provided
    - Highlight important information such as expense amounts, requestors, and approval status
    - Compare multiple PV entries when asked

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
    """;

// [TODO 1] Add the sample PV data list.
// Declare a List<string> variable named samplePvData.
// Each element is a raw JSON string for one PV document (so the list is mutable — status can be updated).
// Include 3 entries:
//   pv-001: Monthly GitHub Copilot Subscription, approval status "Pending", requestor "Somchai Jaidee"
//   pv-002: Conference Registration Fee, approval status "Approved", requestor "Prasert Kaewkla"
//   pv-003: Ergonomic Office Chairs, approval status "Approved", requestor "Wanchai Teeraphon"
List<string> samplePvData = new(); // Replace this line with the full sample data list

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

// [TODO 2] Implement GetPvRequests
// This tool filters samplePvData by approval status and returns matching PV documents as a JSON array.
// Steps:
//   1. Validate that approvalStatus is exactly "Pending" or "Approved"; return an error string if not
//   2. Parse each string in samplePvData with JsonNode.Parse(s) and filter where
//      n["approval"]?["status"]?.GetValue<string>() == approvalStatus
//   3. If no matches found, return a descriptive "not found" message
//   4. Return the matching documents as an indented JSON string using JsonSerializer.Serialize(filtered, ...)
[Description("Retrieve PV requests filtered by their approval status. Call this when the manager asks to see, list, or review PV requests by status. Returns a JSON array of matching PV documents.")]
string GetPvRequests(
    [Description("The approval status to filter by. Must be exactly 'Pending' or 'Approved'.")] string approvalStatus)
{
    // [TODO 2] Replace this stub with the real implementation
    return $"No PV requests found with approval status '{approvalStatus}'. (Implement GetPvRequests to retrieve real data)";
}

// [TODO 3] Implement UpdatePvApprovalStatus
// This tool finds a PV in samplePvData by id, updates its approval.status, and saves it back to the list.
// Steps:
//   1. Validate that newStatus is exactly "Pending" or "Approved"; return an error string if not
//   2. Loop over samplePvData with index (for int i = 0; ...)
//   3. Parse each string with JsonNode.Parse(samplePvData[i])
//   4. Check if node["id"]?.GetValue<string>() == pvId
//   5. If found:
//      a. Read oldStatus from node["approval"]!["status"]!.GetValue<string>()
//      b. Read pvTitle from node["pvTitle"]?.GetValue<string>() ?? pvId
//      c. Set node["approval"]!["status"] = newStatus
//      d. Write back: samplePvData[i] = node.ToJsonString()
//      e. Print: Console.WriteLine($"\n[Update] PV '{pvId}' approval status changed: {oldStatus} → {newStatus}\n")
//      f. Return: $"PV '{pvId}' ({pvTitle}) approval status updated from '{oldStatus}' to '{newStatus}' successfully."
//   6. If not found, return $"PV with id '{pvId}' not found."
[Description("Update the approval status of a specific PV request by its id. Call this when the manager confirms they want to approve a PV or revert it to Pending.")]
string UpdatePvApprovalStatus(
    [Description("The unique id of the PV request to update (e.g. 'pv-001'). Must match the id from GetPvRequests results.")] string pvId,
    [Description("The new approval status. Must be exactly 'Pending' or 'Approved'.")] string newStatus)
{
    // [TODO 3] Replace this stub with the real implementation
    return $"PV with id '{pvId}' not found. (Implement UpdatePvApprovalStatus to update real data)";
}
