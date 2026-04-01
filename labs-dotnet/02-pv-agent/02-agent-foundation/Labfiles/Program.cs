using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

// Add references (already imported above — no additional imports needed)

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
    # Replace this with your PV Agent instruction
    """;

// Create the AIAgent with OpenAI-compatible client and PV Agent instructions
// TODO: Initialize AIAgent using OpenAIClient with ApiKeyCredential and OpenAIClientOptions


// Create an AgentSession to maintain conversation history across turns
// TODO: Create an AgentSession from the agent


// Conversation loop — read user input and stream agent responses
while (true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(userInput)) continue;
    if (userInput.ToLower() == "quit") break;

    Console.Write("\nAgent: ");

    // TODO: Stream the agent response using RunStreamingAsync and print each update
    // Hint: use "await foreach" over agent.RunStreamingAsync(userInput, session)


    Console.WriteLine("\n");
}
