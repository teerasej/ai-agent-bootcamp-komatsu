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

// [TODO 1] Define MA Agent instructions
// Create a string variable named maAgentInstructions that describes the MA Agent's role.
// The agent should help managers review and explore PV requests.
// Include:
//   - YOUR ROLE: summarize, identify issues, answer questions, never approve/reject
//   - UNDERSTANDING PV DATA: show the full PV JSON structure the agent will see
//   - KEY FIELD INTERPRETATIONS: expense.type, approval.status, status values
//   - CONSTRAINTS: never approve, never modify, never invent data
string maAgentInstructions = "# Replace this with your MA Agent instruction";

// [TODO 2] Create the AIAgent using OpenAI-compatible client and MA Agent instructions
// Use OpenAIClient with ApiKeyCredential and the endpoint from appsettings.json.
// Call .GetChatClient(deploymentName).AsAIAgent(instructions: maAgentInstructions, name: "MAAgent")
AIAgent agent = null!;

// [TODO 3] Create an AgentSession to maintain conversation history across turns
// Call await agent.CreateSessionAsync() and assign to a variable named session
AgentSession session = null!;

Console.WriteLine("MA Agent is ready. Paste a PV JSON or ask a question. Type 'quit' to exit.\n");

// Conversation loop — read user input and stream agent responses
while (true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(userInput)) continue;
    if (userInput.ToLower() == "quit") break;

    Console.Write("\nAgent: ");

    // Stream the agent response — call RunStreamingAsync on the agent, passing userInput and session
    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(userInput, session))
    {
        Console.Write(update.Text);
    }

    Console.WriteLine("\n");
}
