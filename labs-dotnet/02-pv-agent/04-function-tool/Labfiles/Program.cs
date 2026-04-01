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
    // TODO: Add a rule: When the user provides a project name, call the get_project_budget tool
    //       to look up the project's total budget and remaining budget.
    //       Use this data to populate project.budgetSummary in the output JSON.

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

// TODO: Define the GetProjectBudget tool function here.
//       - Decorate the method with [Description("...")] to describe what it does
//       - Add [Description("...")] to the projectName parameter as well
//       - Read data/projects_budget.csv, find the matching row
//       - Return a JSON string with exact numeric values, e.g.:
//         {"projectName": "IT Internal", "totalBudget": 100000, "remainingBudget": 80000}
//       Example signature:
//       [Description("Look up the budget and remaining budget for a given project.")]
//       static string GetProjectBudget([Description("The name of the project to look up")] string projectName) { ... }

// Create the AIAgent — TODO: add tools: [AIFunctionFactory.Create(GetProjectBudget)] parameter
AIAgent agent = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: pvAgentInstructions,
        name: "PVAgent");

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

    // Stream the agent response and print each update as it arrives
    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(userInput, session))
    {
        Console.Write(update.Text);
    }

    Console.WriteLine("\n");
}
