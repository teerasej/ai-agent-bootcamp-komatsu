---
lab:
    title: 'D3-M7: PV Agent — Integrate Agent into a Web Chat UI (C#)'
    description: 'Convert the PV Agent CLI app into an ASP.NET Core web application and stream agent responses to a browser-based chat interface using Server-Sent Events (SSE).'
    level: 200
    duration: 30
    islab: true
---

# 7. D3-M7: PV Agent — Integrate Agent into a Web Chat UI (C#)

In this exercise, you'll take the completed PV Agent from lab 06 and integrate it into a minimal **ASP.NET Core** web application. Instead of a console loop, users will interact with the agent through a browser-based chat UI. Agent responses will stream token-by-token to the browser using **Server-Sent Events (SSE)** — no additional libraries required.

This exercise takes approximately **30** minutes.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## 7.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Switch from a Console app (`Microsoft.NET.Sdk`) to a Web app (`Microsoft.NET.Sdk.Web`) without changing agent logic
2. Create and maintain per-session `AgentSession` objects using a `ConcurrentDictionary`
3. Expose a `POST /chat` endpoint that streams agent output as Server-Sent Events (SSE)
4. Understand how `RunStreamingAsync` maps directly from CLI to HTTP streaming

## 7.2. Prerequisites

- Completed **D3-M6** (PV Agent running with Cosmos DB integration, `appsettings.json` configured)
- .NET 8.0 SDK installed and `dotnet build` succeeded in D3-M6

> **Tip**: You can use the `appsettings.json` from your D3-M6 lab — all Azure OpenAI and Cosmos DB settings are identical.

---

## Part 1: Configure the Project

1. In VS Code, navigate to `labs-dotnet/02-pv-agent/07-web-app/Labfiles/`.

1. Copy `appsettings.json.example` and rename it to `appsettings.json`:

    **macOS / Linux:**
    ```bash
    cp appsettings.json.example appsettings.json
    ```

    **Windows (PowerShell):**
    ```powershell
    Copy-Item appsettings.json.example appsettings.json
    ```

1. Open `appsettings.json` and fill in all values — these are the same credentials you used in lab 06:

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "<your Foundry project endpoint>",
        "DeploymentName": "<your model deployment name>",
        "ApiKey": "<your Foundry project API key>"
      },
      "CosmosDB": {
        "ConnectionString": "<your Cosmos DB primary connection string>",
        "DatabaseName": "pv-agent-db",
        "ContainerName": "payment-vouchers"
      }
    }
    ```

1. Save the file.

---

## Part 2: Explore the Project Structure

Open `Labfiles/` in VS Code and take a moment to review the files:

| File | Description |
|---|---|
| `PVAgent.csproj` | Uses `Microsoft.NET.Sdk.Web` — the only change from lab 06's `.csproj` |
| `Program.cs` | Contains the agent instructions and tools from lab 06, plus **three TODO blocks** for you to implement |
| `wwwroot/index.html` | A pre-built chat UI — **no changes needed here** |
| `data/projects_budget.csv` | Same budget data as lab 06 |

> **Observation**: Open `PVAgent.csproj` and compare it with lab 06. The only difference is `<Project Sdk="Microsoft.NET.Sdk.Web">`. This single change unlocks ASP.NET Core hosting, static file serving, and the minimal API routing you'll use in this exercise.

---

## Part 3: Prepare the .NET Project

1. In VS Code, right-click the `Labfiles` folder and select **Open in Integrated Terminal**.

1. Restore packages and verify the project builds:

    **macOS / Linux:**
    ```bash
    dotnet restore
    dotnet build
    ```

    **Windows (PowerShell):**
    ```powershell
    dotnet restore
    dotnet build
    ```

    You should see `Build succeeded` with a few warnings — those warnings are expected. They indicate that the agent and session store aren't wired yet, which is exactly what you'll implement next.

---

## Part 4: Implement TODO 1 — Create the AIAgent

The agent setup is identical to lab 06, with one extra step required by the Web SDK: you must call `.AsIChatClient()` before `.AsAIAgent()` to resolve a type ambiguity introduced by ASP.NET Core's additional `Microsoft.Extensions.AI` dependencies.

1. Open `Program.cs` in VS Code.

1. Find **TODO 1** and replace the comment block with the following code:

    ```csharp
    AIAgent agent = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
        .GetChatClient(deploymentName)
        .AsIChatClient()
        .AsAIAgent(
            instructions: pvAgentInstructions,
            name: "PVAgent",
            tools: [
                AIFunctionFactory.Create(GetProjectBudget),
                AIFunctionFactory.Create(SubmitPv)
            ]);
    ```

    Key point: `.AsIChatClient()` converts the OpenAI `ChatClient` to the `IChatClient` interface that `AsAIAgent` expects when multiple extension method overloads are in scope.

1. Save the file.

---

## Part 5: Implement TODO 2 — Create the Session Store

In the CLI version, a single `AgentSession` was created once and reused for the entire console loop. In the web version, each browser tab needs its own isolated session so conversation history doesn't leak between users.

1. Find **TODO 2** and replace the comment block with the following code:

    ```csharp
    var sessions = new ConcurrentDictionary<string, AgentSession>();
    ```

    Key points:
    - `ConcurrentDictionary` is thread-safe — multiple requests can look up or insert sessions simultaneously without a lock
    - The key is a `sessionId` string generated by the browser and sent with every request
    - Each value is an `AgentSession` that holds the full conversation history for one browser tab

1. Save the file.

---

## Part 6: Implement TODO 3 — Map the `/chat` Endpoint

This is the core of the web integration. The endpoint reads the user's message, runs the agent, and streams each token back to the browser as a Server-Sent Events (SSE) response.

1. Find **TODO 3** and replace the entire comment block with the following code:

    ```csharp
    app.MapPost("/chat", async (HttpContext ctx) =>
    {
        // a) Read the JSON body: { "message": "...", "sessionId": "..." }
        using var reader = new StreamReader(ctx.Request.Body);
        var bodyText = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(bodyText);
        var message   = doc.RootElement.GetProperty("message").GetString()   ?? "";
        var sessionId = doc.RootElement.GetProperty("sessionId").GetString() ?? Guid.NewGuid().ToString();

        // b) Look up an existing session or create a new one for this browser tab
        if (!sessions.TryGetValue(sessionId, out var session))
        {
            session = await agent.CreateSessionAsync();
            sessions[sessionId] = session;
        }

        // c) Configure the response as an SSE stream
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers["Cache-Control"] = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";

        // d) Stream each agent token as an SSE data event
        await foreach (var update in agent.RunStreamingAsync(message, session))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                var data = JsonSerializer.Serialize(update.Text);
                await ctx.Response.WriteAsync($"data: {data}\n\n");
                await ctx.Response.Body.FlushAsync();
            }
        }

        // e) Signal end of stream
        await ctx.Response.WriteAsync("data: [DONE]\n\n");
        await ctx.Response.Body.FlushAsync();
    });
    ```

    Key points:
    - SSE format requires each event to be prefixed with `data: ` and terminated with `\n\n`
    - `JsonSerializer.Serialize(update.Text)` JSON-encodes each token so special characters (quotes, newlines) are safely transmitted
    - `FlushAsync()` after each token ensures the browser receives it immediately rather than waiting for the full response to buffer
    - `data: [DONE]\n\n` is a convention used by the browser-side JavaScript to know when to re-enable the input field

1. Save the file.

---

## Part 7: Run the Web App

1. In the terminal (inside the `Labfiles` folder), run:

    **macOS / Linux:**
    ```bash
    dotnet run
    ```

    **Windows (PowerShell):**
    ```powershell
    dotnet run
    ```

1. You should see output similar to:

    ```
    info: Microsoft.Hosting.Lifetime[14]
          Now listening on: http://localhost:5000
    ```

1. Open your browser and navigate to **http://localhost:5000**.

1. You should see the **PV Agent** chat interface.

---

## Part 8: Test with User Stories

### User Story 1 — Full Data, Successful Submission

Type the following into the chat:

```
I need to create a PV for our monthly GitHub Copilot subscription.
The cost is 5,000 baht per month. Project is IT Internal.
My name is Somchai Jaidee. Payee is GitHub Inc.
Purpose is to support developer productivity tools. Approver is Napat Ruangroj.
```

**Expected behavior:**
- Agent greets and collects any missing fields
- Agent calls `GetProjectBudget` for "IT Internal" automatically
- Agent shows a confirmation summary
- After you confirm, agent calls `SubmitPv` and stores the document in Cosmos DB
- Agent replies that the PV was stored successfully with a UUID

**Verify in the Portal:**
1. Go to your Cosmos DB account in the Azure Portal
2. Open **Data Explorer** → `pv-agent-db` → `payment-vouchers` → **Items**
3. You should see the new document with a UUID `id`

### User Story 2 — Open a Second Browser Tab

1. Open a **new browser tab** and navigate to http://localhost:5000
1. Start a completely different PV conversation in the new tab

**Expected behavior:**
- The new tab starts with a fresh conversation — no history from the first tab
- Each tab maintains its own independent session

> **Observation question**: Why does each tab get its own session? Look at how `sessionId` is stored and used in `wwwroot/index.html`.

### User Story 3 — Refresh and Resume

1. Start a conversation in the browser and provide a few pieces of information (e.g., your name and the project)
1. Refresh the page (F5)
1. Continue the conversation

**Expected behavior:**
- After refresh, the chat UI is blank (visual history is not persisted)
- However, the agent **resumes the same session** — it still remembers the information you provided before refreshing

> **Explanation**: The `sessionId` is stored in `localStorage`, so it survives page refreshes. The `AgentSession` in the server's `ConcurrentDictionary` is looked up by that same ID, restoring the full conversation context.

---

## Deliverables

After completing this exercise, you should have:

- ✅ `appsettings.json` configured with Azure OpenAI and Cosmos DB credentials
- ✅ **TODO 1** implemented: `AIAgent` created with `.AsIChatClient().AsAIAgent(...)` and both tools registered
- ✅ **TODO 2** implemented: `ConcurrentDictionary<string, AgentSession>` session store created
- ✅ **TODO 3** implemented: `POST /chat` endpoint streaming agent tokens as SSE
- ✅ Web app running at `http://localhost:5000` with a working chat UI
- ✅ At least one PV submitted from the browser and visible in Cosmos DB Data Explorer

> **Reference**: If you get stuck, compare your `Program.cs` with the completed solution in `Labfiles-finish/Program.cs`.
