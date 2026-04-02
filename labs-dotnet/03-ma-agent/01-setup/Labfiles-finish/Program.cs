using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

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
    - Compare multiple PV entries when asked

    UNDERSTANDING PV DATA:
    The PV data follows this JSON structure:
    {
      "pv": {
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
    }

    KEY FIELD INTERPRETATIONS:
    - expense.type "MonthlyFee": Recurring monthly payment
    - expense.type "OneTime": Single one-time payment
    - approval.status "Pending": Awaiting manager approval
    - approval.status "Approved": Already approved
    - status "Draft": Incomplete or not yet submitted
    - status "ReadyForSubmission": Complete and ready for manager review

    CONSTRAINTS:
    - You do NOT approve or reject PV requests
    - You do NOT modify PV data
    - You do NOT create new PV requests
    - If PV data is incomplete, note which fields are missing but do not fill them in
    - Always base your analysis on the exact data provided — do not invent or assume unreported values
    """;

// Create the AIAgent using OpenAI-compatible client and MA Agent instructions
AIAgent agent = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: maAgentInstructions,
        name: "MAAgent");

// Create an AgentSession to maintain conversation history across turns
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine("MA Agent is ready. Paste a PV JSON or ask a question. Type 'quit' to exit.\n");

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
