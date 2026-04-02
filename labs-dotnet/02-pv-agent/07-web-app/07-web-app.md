---
lab:
    title: 'D3-M7: PV Agent — Blazor Web Chat UI (C#)'
    description: 'Integrate the PV Agent into an ASP.NET Blazor Server web application so users can interact with the agent through a browser-based chat interface.'
    level: 200
    duration: 30
    islab: true
---

# 7. D3-M7: PV Agent — Blazor Web Chat UI (C#)

In this exercise, you'll take the existing PV Agent (including the Cosmos DB submission from exercise 06) and wrap it in an **ASP.NET Blazor Server** web application. Users will chat with the agent through a simple web-based UI instead of the console.

> **Note**: The goal is to focus on **integrating the Agent into the web UI flow**, not on building a polished front-end.

## 7.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Create a Blazor Server project that hosts the PV Agent
2. Register the `PVAgentService` in the ASP.NET dependency injection container
3. Call the agent from a Blazor component and display the response in a chat interface
4. Understand how `AIAgent` and `AgentSession` map to a scoped web service

## 7.2. Prerequisites

- Completed **D3-M6** (PV Agent with Cosmos DB integration working)
- .NET 8.0 SDK installed
- Azure OpenAI and Cosmos DB credentials from previous exercises

---

## Part 1: Project Setup

### Open the starter project

1. In VS Code, navigate to `labs-dotnet/02-pv-agent/07-web-app/Labfiles/`.

2. Open `appsettings.json.example`. Copy it and rename it to `appsettings.json`:

    ```bash
    cp appsettings.json.example appsettings.json
    ```

3. Fill in your Azure OpenAI and Cosmos DB credentials (same values from exercise 06):

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "<your-endpoint>",
        "DeploymentName": "<your-deployment>",
        "ApiKey": "<your-api-key>"
      },
      "CosmosDB": {
        "ConnectionString": "<your-cosmos-connection-string>",
        "DatabaseName": "pv-agent-db",
        "ContainerName": "payment-vouchers"
      }
    }
    ```

### Explore the project structure

Take a moment to review the project files:

| File | Purpose |
|---|---|
| `PVAgentWeb.csproj` | Blazor Web App project with AI agent and Cosmos DB packages |
| `Program.cs` | ASP.NET host setup with Blazor services |
| `Services/PVAgentService.cs` | Wraps the AIAgent — has TODO sections to complete |
| `Components/Pages/Chat.razor` | Chat UI page — has a TODO to wire up the agent call |
| `Components/App.razor` | Root Blazor component |
| `data/projects_budget.csv` | Budget lookup data (same as exercise 06) |

---

## Part 2: Register the Agent Service

Open `Program.cs`. You'll see a TODO comment — register `PVAgentService` as a **scoped** service so each Blazor circuit (user session) gets its own agent instance:

```csharp
builder.Services.AddScoped<PVAgentService>();
```

> **Why scoped?** Each Blazor Server circuit represents one user's connection. A scoped service ensures each user gets their own `AgentSession` with separate conversation history.

---

## Part 3: Create the AIAgent in PVAgentService

Open `Services/PVAgentService.cs`. You'll find three TODO sections in this file.

### 3.1 — Create the AIAgent

In the constructor, replace the `_agent = null!;` line with code that creates the agent, just like you did in the console app:

```csharp
_agent = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: PVAgentInstructions,
        name: "PVAgent",
        tools: [
            AIFunctionFactory.Create(GetProjectBudget),
            AIFunctionFactory.Create(SubmitPv)
        ]);
```

### 3.2 — Create the AgentSession

Replace the `_session = null!;` line:

```csharp
_session = _agent.CreateSessionAsync().GetAwaiter().GetResult();
```

> We use `.GetAwaiter().GetResult()` because C# constructors cannot be `async`. This runs once when the service is created.

### 3.3 — Implement SendMessageAsync

Replace the stub return in `SendMessageAsync` with the streaming logic:

```csharp
public async Task<string> SendMessageAsync(string userMessage)
{
    var sb = new StringBuilder();
    await foreach (var update in _agent.RunStreamingAsync(userMessage, _session))
    {
        sb.Append(update.Text);
    }
    return sb.ToString();
}
```

This collects the streamed agent response into a single string to display in the UI.

---

## Part 4: Wire Up the Chat Page

Open `Components/Pages/Chat.razor`. Inside the `SendMessage()` method, find the TODO comment and add the agent call:

```csharp
var response = await AgentService.SendMessageAsync(userText);
_messages.Add(new ChatMessage(false, response));
```

This sends the user's message to the agent service and adds the response to the chat history.

---

## Part 5: Build and Run

1. Restore packages and build:

    ```bash
    dotnet build
    ```

2. Run the application:

    ```bash
    dotnet run
    ```

3. Open the URL shown in the terminal (typically `http://localhost:5xxx`) in your browser.

4. You should see the **PV Agent — Web Chat** page with an input field at the bottom.

5. Try typing a message like:

    > I'd like to create a payment voucher

6. The agent will respond in the chat, guiding you through the PV creation flow — the same experience as the console app, but now in a web browser.

7. Complete a full PV submission to verify that the Cosmos DB integration still works.

---

## Summary

In this exercise, you:

- Created a Blazor Server web app that wraps the PV Agent
- Registered `PVAgentService` as a scoped DI service so each user gets their own conversation session
- Called `AgentService.SendMessageAsync()` from a Razor component to bridge the UI and agent
- Verified the complete flow: chat input → agent processing → tool calls (budget lookup + Cosmos DB submission) → response displayed in browser

The key takeaway is that integrating an `AIAgent` into a web application requires minimal changes — wrap it in a service, register it in DI, and call it from your UI component.
