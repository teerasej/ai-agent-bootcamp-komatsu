---
lab:
    title: 'D4-M3: MA Agent — Connect Function Tools to Azure Cosmos DB (C#)'
    description: 'Replace the in-memory stub responses in the MA Agent function tools with real Azure Cosmos DB queries and updates, using the same Cosmos DB container that the PV Agent writes to.'
    level: 200
    duration: 30
    islab: true
---

# D4-M3: MA Agent — Connect Function Tools to Azure Cosmos DB (C#)

In this exercise, you'll replace the **stub responses** in the MA Agent's two function tools with real **Azure Cosmos DB** operations. The MA Agent will now read PV requests from and update PV requests in the same `payment-vouchers` container that the PV Agent writes to.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## Learning Objectives

By the end of this exercise, you'll be able to:

1. Connect a C# application to Azure Cosmos DB using `CosmosClient` with a connection string
2. Query all documents from a Cosmos DB container using the stream API and filter results in C#
3. Read and replace a document in Cosmos DB to update its `approval.status` field
4. Verify that the MA Agent's tools interact correctly with the same data the PV Agent produces

## Prerequisites

- Completed **D3-M6** (Azure Cosmos DB Serverless account provisioned with `pv-agent-db` database and `payment-vouchers` container)
- At least one PV document submitted by the PV Agent in Cosmos DB (from D3-M6 testing)
- Microsoft Foundry project endpoint, deployment name, and API key
- Your Cosmos DB primary connection string (from the Azure Portal)

## Scenario

The MA Agent already has two function tools wired into the agent: `GetPvRequests` and `UpdatePvApprovalStatus`. In the starter project, both tools return stub messages — they don't actually touch Cosmos DB yet. This was fine for validating the tool pattern, but managers need to see **real data** — the actual PV requests submitted by team members through the PV Agent.

In this exercise, you'll wire both tools to the same Azure Cosmos DB `payment-vouchers` container. After this, the full workflow is connected end-to-end:

```
PV Agent (D3)                         MA Agent (D4)
    ↓  SubmitPv → CreateItemStreamAsync      ↑  GetPvRequests → GetItemQueryStreamIterator
Azure Cosmos DB                             ↑  UpdatePvApprovalStatus → ReplaceItemStreamAsync
(payment-vouchers container) ───────────────┘
```

---

## Part 1: Configure the Project

### Set up appsettings.json

1. In VS Code, navigate to `labs-dotnet/03-ma-agent/03-cosmos-db/Labfiles/`.

1. Copy `appsettings.json.example` to `appsettings.json` and fill in your values:

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "https://your-project-endpoint/openai/v1",
        "DeploymentName": "your-model-deployment-name",
        "ApiKey": "your-api-key"
      },
      "CosmosDB": {
        "ConnectionString": "AccountEndpoint=https://your-cosmos-account.documents.azure.com:443/;AccountKey=your-key;",
        "DatabaseName": "pv-agent-db",
        "ContainerName": "payment-vouchers"
      }
    }
    ```

    > **Tip**: The `Endpoint` and `ApiKey` are the same values used in previous exercises. Find them in the Microsoft Foundry portal under your project → **Overview**.

    > **Tip**: The `ConnectionString` is the **PRIMARY CONNECTION STRING** from your Cosmos DB account in the Azure Portal: **Cosmos DB account → Keys → Primary Connection String**.

    > **Tip**: `DatabaseName` and `ContainerName` should match what you created in D3-M6: `pv-agent-db` and `payment-vouchers`.

---

## Part 2: Set Up the .NET Environment

### Restore packages and verify the build

1. Right-click the `Labfiles/` folder and select **Open in Integrated Terminal**.

1. Run the following commands to restore NuGet packages and build the project:

    **macOS / Linux:**
    ```bash
    dotnet restore
    dotnet build
    dotnet run
    ```

    **Windows (PowerShell):**
    ```powershell
    dotnet restore
    dotnet build
    dotnet run
    ```

    > **Note**: The project includes `Microsoft.Azure.Cosmos 3.38.0` for Cosmos DB operations and `Microsoft.Agents.AI.OpenAI` for the MA Agent.

1. When the app starts you should see:

    ```
    MA Agent - Manager Assistant
    ========================================
    Type 'quit' to exit
    ```

1. Try asking `Show me all pending PV requests` — the agent will respond but the tools return stub messages. You'll fix that in the next parts. Type `quit` to stop the app.

---

## Part 3: Review the Starter Code

1. Open `Program.cs` in the `Labfiles/` folder.

1. Notice that the file already has:
    - The MA Agent instruction with full PV data schema and tool-calling rules
    - `GetCosmosContainer()` helper — creates a `CosmosClient` from the connection string and returns the container client
    - `GetPvRequests` — defined with a `[Description]` and the right parameters, but currently returns a stub message
    - `UpdatePvApprovalStatus` — defined similarly with a stub
    - The agent creation with both tools registered via `AIFunctionFactory.Create`
    - The conversation loop with session and streaming

1. Your task: replace the **two `// [TODO]` sections** with real Cosmos DB operations.

---

## Part 4: Add the Cosmos DB Import

The `using Microsoft.Azure.Cosmos;` statement is already at the top of `Program.cs`. No additional imports are required — the SDK is referenced in `MAAgent.csproj`.

---

## Part 5: Implement `GetPvRequests` with Cosmos DB Query

1. Find the `GetPvRequests` method. It currently has a `// [TODO]` placeholder.

1. Replace the `// [TODO] Replace with Cosmos DB query implementation` line (and the return stub below it) with the following implementation:

    ```csharp
    try
    {
        var container = GetCosmosContainer();
        var query = new QueryDefinition("SELECT * FROM c");
        var results = new List<JsonNode>();

        // Use the stream API — each page is a JSON object with a "Documents" array
        using var iterator = container.GetItemQueryStreamIterator(query);
        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync(ct);
            var page = await JsonNode.ParseAsync(response.Content, cancellationToken: ct);
            var documents = page?["Documents"]?.AsArray();
            if (documents == null) continue;

            foreach (var doc in documents)
            {
                if (doc == null) continue;
                var status = doc["approval"]?["status"]?.GetValue<string>();
                if (status == approvalStatus)
                    results.Add(JsonNode.Parse(doc.ToJsonString())!);
            }
        }

        if (results.Count == 0)
            return $"No PV requests found with approval status '{approvalStatus}'.";

        Console.WriteLine($"\n[Cosmos DB] Found {results.Count} PV(s) with status '{approvalStatus}'\n");

        var json = System.Text.Json.JsonSerializer.Serialize(
            results,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return json;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[Cosmos DB] Query failed: {ex.Message}\n");
        return $"Failed to retrieve PV requests: {ex.Message}";
    }
    ```

    Key points:
    - `GetItemQueryStreamIterator` fetches documents using the stream API — each page arrives as a JSON object with a `"Documents"` array
    - `JsonNode.ParseAsync` parses each page's stream content without allocating a typed model
    - Filtering is done in C# by checking `approval.status` — this keeps the SQL query simple
    - Results are re-serialized as an indented JSON string for the agent to interpret
    - Errors are caught and returned as strings so the agent can report them gracefully

1. Save the file (**Ctrl+S**).

---

## Part 6: Implement `UpdatePvApprovalStatus` with Cosmos DB Read + Replace

1. Find the `UpdatePvApprovalStatus` method. It currently has a `// [TODO]` placeholder.

1. Replace the `// [TODO] Replace with Cosmos DB read + update + replace implementation` line (and the return stub below it) with the following implementation:

    ```csharp
    try
    {
        var container = GetCosmosContainer();

        // Read the document as a stream (partition key is /id — same value as the document id)
        using var readResponse = await container.ReadItemStreamAsync(pvId, new PartitionKey(pvId), cancellationToken: ct);
        var document = (JsonObject)(await JsonNode.ParseAsync(readResponse.Content, cancellationToken: ct))!;

        // Update approval.status
        var approval = document["approval"]?.AsObject();
        if (approval == null)
            return $"PV '{pvId}' does not have an approval field.";

        string oldStatus = approval["status"]?.GetValue<string>() ?? "Unknown";
        approval["status"] = newStatus;

        // Replace the document via stream API (avoids SDK serializer conflicts)
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString());
        using var stream = new MemoryStream(bytes);
        await container.ReplaceItemStreamAsync(stream, pvId, new PartitionKey(pvId), cancellationToken: ct);

        string pvTitle = document["pvTitle"]?.GetValue<string>() ?? "Unknown";
        Console.WriteLine($"\n[Cosmos DB] PV '{pvId}' approval status changed: {oldStatus} → {newStatus}\n");
        return $"PV '{pvId}' ({pvTitle}) approval status updated from '{oldStatus}' to '{newStatus}' successfully.";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[Cosmos DB] Update failed: {ex.Message}\n");
        return $"Failed to update PV '{pvId}': {ex.Message}";
    }
    ```

    Key points:
    - `ReadItemStreamAsync(pvId, new PartitionKey(pvId))` — both arguments use the same `pvId` value because the container uses `/id` as the partition key (matching what the PV Agent wrote)
    - `JsonNode.ParseAsync` parses the raw stream and `(JsonObject)` casts it to a mutable object
    - `approval["status"] = newStatus` modifies the node in-place
    - `ReplaceItemStreamAsync` with the serialized bytes replaces the full document in Cosmos DB using the stream API — this avoids type-system conflicts between the Cosmos SDK's serializer and `System.Text.Json.Nodes`
    - The old and new status are logged to the terminal for observability

1. Save the file (**Ctrl+S**).

---

## Part 7: Run the Agent

1. In the terminal, run:

    **macOS / Linux:**
    ```bash
    dotnet run
    ```

    **Windows (PowerShell):**
    ```powershell
    dotnet run
    ```

    > **Tip**: If you see a `CosmosDB:ConnectionString` error, verify the value in `appsettings.json`.

1. When the agent starts, you should see:

    ```
    MA Agent - Manager Assistant
    ========================================
    Type 'quit' to exit
    ```

---

## Part 8: Test with Manager Scenarios

> **Important**: Before testing, ensure you have at least one PV document in Cosmos DB from the PV Agent (D3-M6). If your container is empty, go back and run the PV Agent to submit a PV first.

### Scenario 1 — View Pending PVs and Approve One

At the `You:` prompt, enter:

```
Show me all pending PV requests that need my approval.
```

**Expected behavior:**
- Agent calls `GetPvRequests` with `approvalStatus="Pending"`
- Terminal prints: `[Cosmos DB] Found X PV(s) with status 'Pending'`
- Agent displays a readable summary of the pending PV(s) — titles, amounts, requestors, etc.

Then follow up in the same session:

```
Please approve the first PV in the list.
```

**Expected behavior:**
- Agent calls `UpdatePvApprovalStatus` with the PV's `id` and `newStatus="Approved"`
- Terminal prints: `[Cosmos DB] PV '<id>' approval status changed: Pending → Approved`
- Agent confirms the approval was successful

**Verify in the Azure Portal:**
1. Go to your Cosmos DB account in the Azure Portal
2. Open **Data Explorer** → `pv-agent-db` → `payment-vouchers` → **Items**
3. Find the document by its id — the `approval.status` should now be `"Approved"`

### Scenario 2 — View All Approved PVs

Type `quit` to exit, then restart (`dotnet run`) to start a fresh conversation. At the `You:` prompt, enter:

```
Show me all approved PV requests.
```

**Expected behavior:**
- Agent calls `GetPvRequests` with `approvalStatus="Approved"`
- Agent displays a summary of all approved PVs, including the one you approved in Scenario 1
- If no approved PVs exist yet, the agent should report that none were found

> **Observation question**: Does the PV you approved in Scenario 1 now appear in the approved list? This confirms the full read → update → read cycle works end-to-end with Cosmos DB.

---

## Deliverables

After completing this exercise, you should have:

- ✅ `appsettings.json` configured with `CosmosDB:ConnectionString`, `CosmosDB:DatabaseName`, and `CosmosDB:ContainerName`
- ✅ `GetPvRequests` wired to `GetItemQueryStreamIterator` with C#-side filtering by `approval.status`
- ✅ `UpdatePvApprovalStatus` wired to `ReadItemStreamAsync` + `ReplaceItemStreamAsync`
- ✅ Verified the approve flow end-to-end: pending → approved → confirmed in Data Explorer

---

## Summary

In this exercise, you:

- Connected the MA Agent's function tools to the same Azure Cosmos DB container used by the PV Agent
- Implemented `GetPvRequests` using `GetItemQueryStreamIterator` and `JsonNode` to query and filter PV documents by approval status
- Implemented `UpdatePvApprovalStatus` using `ReadItemStreamAsync` and `ReplaceItemStreamAsync` to update a document's `approval.status` field in-place
- Tested the full manager workflow: view pending requests, approve one, then verified it appears in the approved list

The MA Agent now has a live connection to the PV data store, completing the end-to-end PV Agent → Cosmos DB → MA Agent workflow.
