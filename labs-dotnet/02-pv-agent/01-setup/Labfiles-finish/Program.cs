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

Console.WriteLine("Testing connection to Foundry project...");
Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine($"Model: {deploymentName}");
Console.WriteLine();

// Use OpenAI client with Foundry project's OpenAI-compatible endpoint
AIAgent agent = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "You are a helpful assistant.",
        name: "SmokeTestAgent");

var response = await agent.RunAsync(
    "Hello! Are you available? Please confirm you are working correctly.");

// Print the response and confirm success
Console.WriteLine($"Agent response: {response}");
Console.WriteLine("\nSmoke test PASSED: Agent is working correctly!");
