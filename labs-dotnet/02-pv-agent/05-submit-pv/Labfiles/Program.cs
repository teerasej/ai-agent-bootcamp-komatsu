using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;

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
    - When the user provides a project name, MUST call the GetAllProjectBudgets tool to retrieve the complete list of projects and their budgets. Use this data to validate the project name and populate project.budgetSummary in the output JSON with the exact numeric values returned by the tool. Do NOT invent or default these values.
    // TODO: Add two SubmitPv gating rules here:
    //   1. Completeness gate — do NOT call SubmitPv until all required fields are filled
    //      and status is "ReadyForSubmission" (no placeholders, no zeros for required amounts)
    //   2. Trigger rule — call SubmitPv immediately after the user confirms the summary

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
    - Set status to "ReadyForSubmission" only after user confirms the summary
    - Keep status as "Draft" otherwise

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

// Define the GetAllProjectBudgets tool function
[Description("Retrieve all projects and their budget information from the CSV data file. Call this when the user provides a project name to validate it and get budget figures.")]
static string GetAllProjectBudgets()
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

    var projects = new System.Text.StringBuilder("[");
    bool first = true;
    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i])) continue;
        string[] fields = lines[i].Split(',');
        string name      = nameIdx   < fields.Length ? fields[nameIdx].Trim()   : "";
        string budget    = budgetIdx < fields.Length ? fields[budgetIdx].Trim()  : "0";
        string remaining = remainIdx < fields.Length ? fields[remainIdx].Trim()  : "0";
        if (!first) projects.Append(',');
        projects.Append($"{{\"projectName\":\"{name}\",\"totalBudget\":{budget},\"remainingBudget\":{remaining}}}");
        first = false;
    }
    projects.Append(']');
    return projects.ToString();
}

// TODO: Define the SubmitPv tool function here.
//   - Add [Description("...")] on the method: describe that it submits the completed PV
//     once the user has confirmed all fields. Make it clear: call immediately after confirmation
//     when status is "ReadyForSubmission".
//   - Add [Description("...")] on the pvJson parameter: the complete PV JSON string,
//     status must be "ReadyForSubmission", all required fields filled.
//   - In the body: print the JSON to the console between separator lines, then return a
//     confirmation string such as "PV submission received and ready for database insertion."
//   Example signature:
//   [Description("Submit the completed PV once the user has confirmed all fields...")]
//   static string SubmitPv([Description("The complete PV JSON...")] string pvJson) { ... }

// Create the AIAgent — TODO: add AIFunctionFactory.Create(SubmitPv) to the tools list
AIAgent agent = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: pvAgentInstructions,
        name: "PVAgent",
        tools: [AIFunctionFactory.Create(GetAllProjectBudgets)]);

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
