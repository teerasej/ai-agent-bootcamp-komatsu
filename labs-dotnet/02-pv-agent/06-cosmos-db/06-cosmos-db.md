---
lab:
    title: 'D3-M6: PV Agent — Store PV Data in Azure Cosmos DB (C#)'
    description: 'Provision an Azure Cosmos DB Serverless account, then implement the SubmitPv function tool to insert completed PV JSON documents into a Cosmos DB container using the Microsoft Agent Framework in C#.'
    level: 200
    duration: 45
    islab: true
---

# 6. D3-M6: PV Agent — Store PV Data in Azure Cosmos DB (C#)

In this exercise, you'll provision an **Azure Cosmos DB for NoSQL** account using the **Serverless** capacity mode in the Azure Portal, then wire up the `SubmitPv` tool in the PV Agent to actually insert each completed Payment Voucher into that database.

This exercise takes approximately **45** minutes.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## 6.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Create an Azure Cosmos DB for NoSQL account with Serverless capacity in the Azure Portal
2. Create a database and container with an appropriate partition key
3. Retrieve the Cosmos DB connection string and add it to `appsettings.json`
4. Use the `Microsoft.Azure.Cosmos` C# SDK to insert a document from an `AIFunctionFactory` tool
5. Use `JsonNode` to parse agent JSON output and add a unique document `id`
6. Verify that the PV document appears in the Data Explorer after submission

## 6.2. Prerequisites

- Completed **D3-M4** (PV Agent running with `GetProjectBudget` tool, `appsettings.json` configured)
- .NET 8.0 SDK installed and `dotnet build` succeeded in D3-M4
- An active [Azure subscription](https://azure.microsoft.com/free/)

---

## Part 1: Create an Azure Cosmos DB Serverless Account

Serverless mode bills only for the request units (RUs) your operations consume, with no minimum throughput charge — ideal for development and low-traffic workloads.

### Create the resource

1. Sign in to the [Azure Portal](https://portal.azure.com).

1. In the top search bar, type **Azure Cosmos DB** and select it from the results.

1. Select **+ Create**.

1. On the **Select API option** page, choose **Azure Cosmos DB for NoSQL** and select **Create**.

1. Fill in the **Basics** tab:

   | Field | Value |
   |---|---|
   | **Workload Type** | Learning |
   | **Subscription** | Your Azure subscription |
   | **Resource Group** | Use the same resource group as your Foundry project (e.g., `rg-XX`) |
   | **Account Name** | Enter a globally unique name, e.g., `pv-agent-cosmos-[yourname]` |
   | **Location** | Choose the same region as your Foundry project |
   | **Capacity mode** | Select **Serverless** |

1. Leave all other settings as default and select **Review + create**.

1. Review the configuration and select **Create**. Deployment takes approximately 2–3 minutes.

1. When the deployment is complete, select **Go to resource**.

### Create the database and container

1. In the left navigation of your Cosmos DB account, select **Data Explorer**.

1. Select **New Container**.

1. Fill in the **New Container** panel:

   | Field | Value |
   |---|---|
   | **Database id** | Select **Create new**, enter `pv-agent-db` |
   | **Container id** | `payment-vouchers` |
   | **Partition key** | `/id` |

1. Select **OK**. The new database and container appear in the Data Explorer tree.

### Copy the connection string

1. In the left navigation, select **Keys**.

1. Under **PRIMARY CONNECTION STRING**, select the copy icon to copy the connection string to your clipboard.

   > **Important**: Treat this connection string as a secret — do not commit it to source control. The `.gitignore` in this project already excludes `appsettings.json`.

---

## Part 2: Configure the Project

1. In VS Code, navigate to `labs-dotnet/02-pv-agent/06-cosmos-db/Labfiles/`.

1. Open `appsettings.json.example`. You'll see a new `CosmosDB` section alongside the existing `AzureOpenAI` settings.

1. Copy the file and rename it to `appsettings.json`:

    **macOS / Linux:**
    ```bash
    cp appsettings.json.example appsettings.json
    ```

    **Windows (PowerShell):**
    ```powershell
    Copy-Item appsettings.json.example appsettings.json
    ```

1. Open `appsettings.json` in VS Code and fill in all values:

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

    > **Note**: `DatabaseName` and `ContainerName` must match exactly what you entered in the Azure Portal.

1. Save the file.

---

## Part 3: Prepare the .NET Project

1. In VS Code, right-click the `Labfiles` folder and select **Open in Integrated Terminal**.

1. Run the following commands to restore packages and verify the project builds:

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

    The `Microsoft.Azure.Cosmos` package is included in `PVAgent.csproj` for this exercise and will be downloaded automatically by `dotnet restore`.

---

## Part 4: Implement the `SubmitPv` Tool with Cosmos DB Insertion

The `SubmitPv` function already exists in `Program.cs` as a stub — it currently only prints the JSON to the console. You'll replace the stub body with real Cosmos DB insertion logic.

The stub currently looks like this:

```csharp
[Description("Insert the completed PV JSON into Azure Cosmos DB once the user has confirmed all fields.")]
async Task<string> SubmitPv(
    [Description("The complete PV JSON object as a string, with status set to ReadyForSubmission and all required fields filled")]
    string pvJson)
{
    var connectionString = configuration["CosmosDB:ConnectionString"]
        ?? throw new InvalidOperationException("Set CosmosDB:ConnectionString in appsettings.json");
    var databaseName = configuration["CosmosDB:DatabaseName"] ?? "pv-agent-db";
    var containerName = configuration["CosmosDB:ContainerName"] ?? "payment-vouchers";

    // [TODO] Replace this stub with Cosmos DB insertion logic:
    // ...
    return await Task.FromResult("PV submission received. Cosmos DB insertion not yet implemented.");
}
```

### Replace the stub body

1. Open `Program.cs` in VS Code.

1. Find the `[TODO]` block inside `SubmitPv` and replace the entire stub body with the following:

    ```csharp
    try
    {
        // Parse the JSON string from the agent
        var root = JsonNode.Parse(pvJson)!;

        // Extract the inner "pv" object if the agent wrapped it; otherwise use the root
        var document = (root["pv"] as JsonObject) ?? (JsonObject)root!;

        // Cosmos DB requires a unique "id" field at the document root
        string newId = Guid.NewGuid().ToString();
        document["id"] = newId;

        // Connect to Cosmos DB using the primary connection string
        using var cosmosClient = new CosmosClient(connectionString);
        var container = cosmosClient.GetDatabase(databaseName).GetContainer(containerName);

        // Serialize the document and insert via stream API
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString());
        using var stream = new MemoryStream(bytes);
        await container.CreateItemStreamAsync(stream, new PartitionKey(newId));

        Console.WriteLine($"\n[Cosmos DB] Document inserted with id: {newId}\n");
        return $"PV submitted successfully and stored in Azure Cosmos DB with id: {newId}";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[Cosmos DB] Insertion failed: {ex.Message}\n");
        return $"PV submission failed: {ex.Message}";
    }
    ```

    Key points:
    - `JsonNode.Parse(pvJson)` parses the agent's JSON output string into a navigable tree
    - `root["pv"] as JsonObject` safely unwraps the `"pv"` wrapper the agent produces, falling back to the root if the wrapper is absent
    - `Guid.NewGuid().ToString()` generates a UUID as the document `id` — this matches the partition key path `/id` you configured in the portal
    - `CosmosClient(connectionString)` creates a client using the primary connection string — no separate credential object is needed
    - `CreateItemStreamAsync` accepts a raw `Stream` of UTF-8 JSON bytes, avoiding any type-system conflicts with the Cosmos SDK serializer
    - Errors are caught and returned as a string so the agent can report failures to the user gracefully

1. Save the file (**Ctrl+S** / **Cmd+S**).

---

## Part 5: Run the Agent

1. In the terminal (inside the `Labfiles` folder), run:

    **macOS / Linux:**
    ```bash
    dotnet run
    ```

    **Windows (PowerShell):**
    ```powershell
    dotnet run
    ```

1. Test using the user stories below.

---

## Part 6: Test with User Stories

### User Story 1 — Full Data, Successful Submission

```
I need to create a PV for our monthly GitHub Copilot subscription.
The cost is 5,000 baht per month. Project is IT Internal.
My name is Somchai Jaidee. Payee is GitHub Inc.
Purpose is to support developer productivity tools. Approver is Napat Ruangroj.
```

**Expected behavior:**
- Agent collects all fields, calls `GetProjectBudget` for "IT Internal"
- Agent shows a confirmation summary
- After you confirm, agent calls `SubmitPv`
- Terminal prints: `[Cosmos DB] Document inserted with id: <uuid>`
- Agent replies that the PV was stored successfully

**Verify in the Portal:**
1. Go to your Cosmos DB account in the Azure Portal
2. Open **Data Explorer** → `pv-agent-db` → `payment-vouchers` → **Items**
3. You should see the new document with the UUID as its `id`

### User Story 2 — One-Time Equipment Purchase

```
I want to submit a PV to purchase 5 ergonomic chairs for the HR team.
Total cost is 25,000 baht. This is for the HR Internal project.
My name is Wanchai Teeraphon. Payee is Office Furniture Co., Ltd.
Purpose is to improve workspace comfort. Approver is Manee Srisuk.
```

**Expected behavior:**
- Agent identifies `expense.type` as `OneTime` and derives `expense.budgetType` as `"Investment"`
- After confirmation, agent calls `SubmitPv` and inserts the document into Cosmos DB
- A second document appears in Data Explorer

### User Story 3 — Partial Data, Agent Asks for Missing Fields

```
Please create a PV for a training workshop, 3,500 baht.
```

**Expected behavior:**
- Agent asks for all missing fields one at a time
- After all fields are provided and you confirm, agent calls `SubmitPv`
- Document is stored in Cosmos DB

> **Observation question**: Open the inserted document in Data Explorer. Does the document structure match the PV JSON the agent produced? Are `totalBudget` and `remainingBudget` populated with the real values from the CSV?

---

## Deliverables

After completing this exercise, you should have:

- ✅ Azure Cosmos DB for NoSQL Serverless account created with `pv-agent-db` database and `payment-vouchers` container
- ✅ Connection string stored in `appsettings.json` as `CosmosDB:ConnectionString`
- ✅ `Microsoft.Azure.Cosmos` NuGet package restored (`dotnet restore` succeeded)
- ✅ `SubmitPv` implemented with `CosmosClient`, `JsonNode` parsing, `Guid.NewGuid()` id generation, and `CreateItemStreamAsync`
- ✅ At least one test PV document visible in Data Explorer with a UUID `id` field
