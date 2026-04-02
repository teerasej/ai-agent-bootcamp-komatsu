---
lab:
    title: 'D4-M2: MA Agent — Function Tools for PV Review and Approval (C#)'
    description: 'Extend the MA Agent with two custom function tools: one to retrieve PV requests filtered by approval status and one to update PV approval status. Uses in-memory sample data as a placeholder for Cosmos DB integration.'
    level: 200
    duration: 30
    islab: true
---

# 2. D4-M2: MA Agent — Function Tools for PV Review and Approval (C#)

In this exercise, you'll extend the MA Agent with two **custom function tools** using `AIFunctionFactory.Create` from the **Microsoft Agent Framework**. These tools give the agent the ability to:

1. **Retrieve** PV requests filtered by approval status (`Pending` or `Approved`)
2. **Update** the approval status of a specific PV request

You'll use in-memory sample data as a placeholder — actual Cosmos DB integration comes in the next exercise.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## 2.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Explain the `[Description]` attribute pattern and how the agent decides when to call a tool
2. Implement a function tool that filters and returns structured JSON data from in-memory storage
3. Implement a function tool that updates in-memory data and returns a confirmation
4. Register multiple tools with the Agent using `AIFunctionFactory.Create`
5. Update the agent instruction to guide tool usage behavior

## 2.2. Prerequisites

- Completed **D4-M1** (MA Agent running with instruction, `appsettings.json` configured)
- Microsoft Foundry VS Code extension installed and connected
- .NET 8 SDK installed

## 2.3. Scenario

Your organization's managers now want the MA Agent to be more than a passive reader — they want to ask the agent for PV requests by status and approve them directly through conversation. In this exercise, you'll build the two tools that enable this workflow:

- `GetPvRequests(approvalStatus)` — retrieves PV entries matching a given approval status
- `UpdatePvApprovalStatus(pvId, newStatus)` — changes a PV's approval status

For now, both tools work against **sample data stored in a `List<string>`**. This lets you focus on the tool pattern and agent instruction tuning without worrying about database connectivity.

---

## Part 1: Understand Function Call Tools

### When to use a tool vs. instruction grounding

| Approach | Best for |
|---|---|
| Instruction grounding | Classification rules, enum derivation, format rules |
| Function call tool | Data lookup, data updates, calculations, external API calls, I/O operations |

The `[Description]` attribute marks a C# method as a capability the agent can invoke. The agent decides *when* to call it based on the method's description, the parameter descriptions, and the conversation context.

### The `[Description]` pattern in C\#

Here is the pattern used in the Microsoft Agent Framework:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

[Description("What this tool does — the agent reads this to decide when to call it.")]
string MyTool(
    [Description("Description of the parameter — guides the agent on what value to pass.")] string paramName)
{
    // tool logic here
    return "result string";
}

// Register the tool when creating the agent
AIAgent agent = chatClient.AsAIAgent(
    instructions: "...",
    name: "MyAgent",
    tools: [AIFunctionFactory.Create(MyTool)]);
```

Key points:
- `[Description("...")]` on the **method** tells the agent what the tool does and when to call it
- `[Description("...")]` on each **parameter** guides the agent on valid values to pass
- `AIFunctionFactory.Create(MyTool)` converts the method into a tool the framework can invoke
- Multiple tools are passed as a collection in the `tools:` parameter of `AsAIAgent()`

### In-memory data storage design

For this exercise, PV data is stored as a `List<string>` where each element is a raw JSON string:

```csharp
List<string> samplePvData =
[
    """{"id":"pv-001", ..., "approval":{"status":"Pending"}, ...}""",
    """{"id":"pv-002", ..., "approval":{"status":"Approved"}, ...}"""
];
```

Using raw JSON strings (rather than a typed C# class) means:
- No need to define a class for the full PV schema
- `JsonNode.Parse(s)` gives you a mutable in-memory tree
- Changes are saved back with `samplePvData[i] = node.ToJsonString()`
- The same pattern scales directly to Cosmos DB stream APIs in the next exercise

---

## Part 2: Open the Starter Code

### Configure the .NET environment

1. In VS Code, navigate to `labs-dotnet/03-ma-agent/02-function-tools/Labfiles/`.

1. Right-click the folder and select **Open in Integrated Terminal**.

1. Run the following commands to restore packages and verify the build:

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

1. Copy `appsettings.json.example` to `appsettings.json` and fill in your values:

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "https://your-project-endpoint/openai/v1",
        "DeploymentName": "gpt-4.1",
        "ApiKey": "your-api-key"
      }
    }
    ```

    > **Tip**: Use the same endpoint and API key from D4-M1. Find them in the Microsoft Foundry portal under your project → **Overview**.

### Open the starter file

Open `Program.cs`. You will see:
- The complete MA Agent configuration from D4-M1
- `samplePvData` declared as an empty `List<string>` (TODO 1)  
- `GetPvRequests` and `UpdatePvApprovalStatus` declared with stub bodies  
- Both tools **already registered** in `AsAIAgent(tools: [...])`
- The agent instruction **without** the tool-calling rules (TODO 4)

---

## Part 3: Add Sample PV Data

Before the tools can filter or update, you need data to work with.

1. Find the comment `// [TODO 1]` in `Program.cs`.

1. Replace the empty initialization with the full sample data list:

    ```csharp
    // Sample PV data — placeholder for Cosmos DB integration in the next exercise.
    // Stored as a mutable List<string> where each element is a raw JSON string (one PV document).
    List<string> samplePvData =
    [
        """{"id":"pv-001","pvTitle":"Monthly GitHub Copilot Subscription","requestDate":"2026-03-01","requestor":{"name":"Somchai Jaidee"},"payee":{"name":"GitHub Inc."},"purpose":{"for":"Monthly developer tool subscription","objective":"Improve developer productivity for IT Internal project"},"expense":{"type":"MonthlyFee","budgetType":"Expense","amount":{"value":5000,"currency":"THB"}},"project":{"projectName":"IT Internal","budgetSummary":{"totalBudget":50000,"remainingBudget":45000}},"approval":{"approverName":"Napat Ruangroj","status":"Pending"},"status":"ReadyForSubmission"}""",
        """{"id":"pv-002","pvTitle":"Conference Registration Fee","requestDate":"2026-03-15","requestor":{"name":"Prasert Kaewkla"},"payee":{"name":"TechConf Thailand"},"purpose":{"for":"Annual developer conference registration","objective":"Team learning and networking"},"expense":{"type":"OneTime","budgetType":"Investment","amount":{"value":3500,"currency":"THB"}},"project":{"projectName":"HR Development","budgetSummary":{"totalBudget":20000,"remainingBudget":16500}},"approval":{"approverName":"Manee Srisuk","status":"Approved"},"status":"ReadyForSubmission"}""",
        """{"id":"pv-003","pvTitle":"Ergonomic Office Chairs","requestDate":"2026-03-20","requestor":{"name":"Wanchai Teeraphon"},"payee":{"name":"Office Furniture Co., Ltd."},"purpose":{"for":"Purchase of 5 ergonomic chairs","objective":"Improve workspace comfort for HR team"},"expense":{"type":"OneTime","budgetType":"Investment","amount":{"value":25000,"currency":"THB"}},"project":{"projectName":"HR Internal","budgetSummary":{"totalBudget":100000,"remainingBudget":75000}},"approval":{"approverName":"Manee Srisuk","status":"Approved"},"status":"ReadyForSubmission"}"""
    ];
    ```

    This gives you **3 PV entries**: 1 with `Pending` status and 2 with `Approved` status.

---

## Part 4: Implement the `GetPvRequests` Tool

1. Scroll to the bottom of `Program.cs` and find the comment `// [TODO 2]` inside the `GetPvRequests` method.

1. Replace the stub `return` line with the real implementation:

    ```csharp
    if (approvalStatus != "Pending" && approvalStatus != "Approved")
        return $"Invalid approval status '{approvalStatus}'. Must be 'Pending' or 'Approved'.";

    // Parse each raw JSON string, filter by approval.status, collect matches
    var filtered = samplePvData
        .Select(s => JsonNode.Parse(s)!)
        .Where(n => n["approval"]?["status"]?.GetValue<string>() == approvalStatus)
        .ToList();

    if (filtered.Count == 0)
        return $"No PV requests found with approval status '{approvalStatus}'.";

    Console.WriteLine($"\n[In-memory] Found {filtered.Count} PV(s) with status '{approvalStatus}'\n");

    return JsonSerializer.Serialize(filtered, new JsonSerializerOptions { WriteIndented = true });
    ```

    Key points:
    - Input is validated before any data access
    - `JsonNode.Parse(s)` parses each raw JSON string into a navigable tree
    - `.Where(n => n["approval"]?["status"]?.GetValue<string>() == approvalStatus)` filters by the nested `approval.status` field
    - `JsonSerializer.Serialize(filtered, ...)` returns a pretty-printed JSON array the agent can read and summarize

---

## Part 5: Implement the `UpdatePvApprovalStatus` Tool

1. Find the comment `// [TODO 3]` inside the `UpdatePvApprovalStatus` method.

1. Replace the stub `return` line with the real implementation:

    ```csharp
    if (newStatus != "Pending" && newStatus != "Approved")
        return $"Invalid status '{newStatus}'. Must be 'Pending' or 'Approved'.";

    for (int i = 0; i < samplePvData.Count; i++)
    {
        var node = JsonNode.Parse(samplePvData[i])!;
        if (node["id"]?.GetValue<string>() != pvId) continue;

        string oldStatus = node["approval"]!["status"]!.GetValue<string>();
        string pvTitle = node["pvTitle"]?.GetValue<string>() ?? pvId;

        // Mutate the node and write back as a JSON string
        node["approval"]!["status"] = newStatus;
        samplePvData[i] = node.ToJsonString();

        Console.WriteLine($"\n[Update] PV '{pvId}' approval status changed: {oldStatus} → {newStatus}\n");
        return $"PV '{pvId}' ({pvTitle}) approval status updated from '{oldStatus}' to '{newStatus}' successfully.";
    }

    return $"PV with id '{pvId}' not found.";
    ```

    Key points:
    - Input validation ensures only valid status values are accepted
    - The loop finds the target by `id` comparison
    - `node["approval"]!["status"] = newStatus` mutates the `JsonNode` in place
    - `samplePvData[i] = node.ToJsonString()` writes the updated JSON string back to the list — the next call to `GetPvRequests` will see the updated status
    - The terminal log line `[Update] PV '...' changed: Pending → Approved` gives you visual confirmation

---

## Part 6: Update the Agent Instruction

The agent needs to know it has tools and when to use them. Add the tool-calling rules to `maAgentInstructions`.

1. Find the `// [TODO 4]` comment block above `maAgentInstructions`.

1. In the `YOUR ROLE` section, add the following two lines **at the end of the role bullet list**:

    ```
    - When the manager asks to see PV requests, ALWAYS call the GetPvRequests tool with the appropriate approval status filter
    - When the manager asks to approve a PV or set it back to pending, call the UpdatePvApprovalStatus tool with the exact PV id and the new status
    ```

1. In the `CONSTRAINTS` section, add the following line **at the end**:

    ```
    - When updating approval status, you MUST use the exact PV id from the data returned by GetPvRequests
    ```

1. Save the file (**Ctrl+S** / **Cmd+S**).

    > **Why this matters**: Without these instruction rules, the agent might try to answer from memory or invent PV data. Explicit rules that name the tools directly and say "ALWAYS call" are the most reliable way to ensure consistent tool invocation.

---

## Part 7: Run the Agent

1. In the terminal (inside `Labfiles/`), run:

    **macOS / Linux:**
    ```bash
    dotnet run
    ```

    **Windows (PowerShell):**
    ```powershell
    dotnet run
    ```

1. When the agent starts, you should see:

    ```
    MA Agent - Manager Assistant
    ========================================
    MA Agent is ready. Ask to see PV requests or type 'quit' to exit.
    ```

---

## Part 8: Test with User Stories

### Scenario 1 — View Pending PV Requests and Approve One

At the `You:` prompt, enter:

```
Show me all pending PV requests that need my review.
```

**Expected behavior:**
- Terminal prints: `[In-memory] Found 1 PV(s) with status 'Pending'`
- Agent presents a summary of the Copilot subscription PV (`pv-001`)
- Agent does NOT invent additional PV entries

Now approve the pending request:

```
Please approve the GitHub Copilot subscription PV.
```

**Expected behavior:**
- Terminal prints: `[Update] PV 'pv-001' approval status changed: Pending → Approved`
- Agent confirms the approval in its response
- The status is now updated in memory — a follow-up query for Pending PVs will return 0

### Scenario 2 — View Approved PV Requests

Type `quit`, restart the agent (`dotnet run`), then enter:

```
Can you show me all PV requests that have already been approved?
```

**Expected behavior:**
- Terminal prints: `[In-memory] Found 2 PV(s) with status 'Approved'`
- Agent summarizes the two approved PVs (Conference Registration Fee and Ergonomic Office Chairs)
- Agent presents relevant details: amounts, requestors, projects

> **Observation**: Does the agent always call the tools to retrieve data, or does it sometimes answer from memory? If it skips the tool, revisit your instruction and strengthen the "ALWAYS call" rule for `GetPvRequests`. Does the agent use the exact `pv_id` when updating? Small wording changes in the instruction can significantly affect tool invocation behaviour.

---

## Deliverables

Before moving to the next exercise, verify:

- [ ] `samplePvData` contains 3 PV entries with `pv-001` as `Pending` and `pv-002`, `pv-003` as `Approved`
- [ ] `GetPvRequests("Pending")` returns only `pv-001`; `GetPvRequests("Approved")` returns `pv-002` and `pv-003`
- [ ] `UpdatePvApprovalStatus("pv-001", "Approved")` prints the `[Update]` log line and confirmation
- [ ] After approving `pv-001`, a follow-up call to `GetPvRequests("Pending")` returns "No PV requests found"
- [ ] The agent does NOT invent PV data — it always calls the tools
