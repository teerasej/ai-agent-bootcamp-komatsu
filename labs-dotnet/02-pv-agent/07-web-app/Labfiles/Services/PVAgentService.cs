using Microsoft.Agents.AI;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;

namespace PVAgentWeb.Services;

public class PVAgentService
{
    private readonly AIAgent _agent;
    private AgentSession? _session;
    private readonly IConfiguration _configuration;

    public PVAgentService(IConfiguration configuration)
    {
        _configuration = configuration;

        // TODO: Read AzureOpenAI settings from configuration
        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Set AzureOpenAI:Endpoint in appsettings.json");
        var deploymentName = configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("Set AzureOpenAI:DeploymentName in appsettings.json");
        var apiKey = configuration["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Set AzureOpenAI:ApiKey in appsettings.json");

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
                var root = JsonNode.Parse(pvJson)!;
                var document = (root["pv"] as JsonObject) ?? (JsonObject)root!;

                string newId = Guid.NewGuid().ToString();
                document["id"] = newId;

                using var cosmosClient = new CosmosClient(connectionString);
                var container = cosmosClient.GetDatabase(databaseName).GetContainer(containerName);

                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString());
                using var stream = new MemoryStream(bytes);
                await container.CreateItemStreamAsync(stream, new PartitionKey(newId));

                return $"PV submitted successfully and stored in Azure Cosmos DB with id: {newId}";
            }
            catch (Exception ex)
            {
                return $"PV submission failed: {ex.Message}";
            }
        }

        // TODO: Create the AIAgent using OpenAIClient.GetChatClient().AsAIAgent()
        //       Pass pvAgentInstructions, name "PVAgent", and register both tool functions
        // _agent = new OpenAIClient(...)
        //     .GetChatClient(deploymentName)
        //     .AsAIAgent(
        //         instructions: pvAgentInstructions,
        //         name: "PVAgent",
        //         tools: [
        //             AIFunctionFactory.Create(GetProjectBudget),
        //             AIFunctionFactory.Create(SubmitPv)
        //         ]);
        throw new NotImplementedException("Create the AIAgent — see the TODO above.");
    }

    /// <summary>
    /// Sends a user message to the PV Agent and returns the full response text.
    /// An AgentSession is lazily created on the first call and reused for the lifetime
    /// of this scoped service instance (one per Blazor circuit / user connection).
    /// </summary>
    public async Task<string> SendMessageAsync(string message)
    {
        // TODO: Lazily create the session on first call
        // _session ??= await _agent.CreateSessionAsync();

        // TODO: Stream the agent response and collect all update.Text values
        // var sb = new StringBuilder();
        // await foreach (AgentResponseUpdate update in _agent.RunStreamingAsync(message, _session))
        // {
        //     sb.Append(update.Text);
        // }
        // return sb.ToString();

        throw new NotImplementedException("Implement SendMessageAsync — see the TODOs above.");
    }
}
