---
lab:
    title: 'D3-M4: PV Agent — Function Call Tool for Budget Data Lookup (C#)'
    description: 'Extend the PV Agent with a custom function tool that reads project budget data from a CSV file using the Microsoft Agent Framework AIFunctionFactory in C#.'
    level: 200
    duration: 30
    islab: true
---

# 4. D3-M4: PV Agent — Function Call Tool for Budget Data Lookup (C#)

In this exercise, you'll extend the PV Agent by implementing a **custom function tool** that looks up real project budget data from a CSV file. You'll use `AIFunctionFactory.Create` from the **Microsoft Agent Framework** and wire the tool into your agent — no changes to the Foundry portal are needed.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## 4.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Explain the difference between instruction-based grounding and function call tools
2. Use `[Description]` attributes on a C# method and parameters to provide agent-readable metadata
3. Use `AIFunctionFactory.Create` to register a C# method as an agent tool
4. Read structured data from a CSV file inside a tool function
5. Pass a list of tools to `AsAIAgent()` and verify the agent calls them automatically
6. Test end-to-end tool invocation with user story scenarios

## 4.2. Prerequisites

- Completed **D3-M3** (PV Agent running with `expense.budgetType`, `appsettings.json` configured)
- .NET 8.0 SDK installed and `dotnet build` succeeded in D3-M3

## 4.3. Scenario

The back-office finance team now requires that each Payment Voucher includes the project's **total budget** and **remaining budget** pulled from the company's project data. This information is stored in a CSV file at `data/projects_budget.csv`.

Unlike the `budgetType` classification from D3-M3 — which could be derived from conversation context — **budget numbers are facts** that must come from a data source. Using a function tool is the correct approach here.

---

## 4.4. Part 1: Understand Function Call Tools

### When to use a tool vs. instruction grounding

| Approach | Best for |
|---|---|
| Instruction grounding | Classification rules, enum derivation, format rules |
| Function call tool | Data lookup, calculations, external API calls, I/O operations |

### The C# function tool pattern

In the Microsoft Agent Framework, any regular **C# method** can become a tool:

1. Decorate the method with `[Description("...")]` from `System.ComponentModel` to explain what it does
2. Decorate each parameter with `[Description("...")]` so the agent knows what to pass
3. Wrap it with `AIFunctionFactory.Create(MethodName)` from `Microsoft.Extensions.AI`
4. Pass the result in the `tools` list of `AsAIAgent()`

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

[Description("Get the weather for a given location.")]
static string GetWeather(
    [Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

AIAgent agent = chatClient.AsAIAgent(
    instructions: "You are a helpful assistant",
    tools: [AIFunctionFactory.Create(GetWeather)]);
```

Key points:
- `[Description]` on the method provides the agent with a natural-language description of what the function does — the agent uses this to decide *when* to call it
- `[Description]` on each parameter tells the agent how to fill in the arguments from the conversation
- The method returns a `string` that the agent reads and uses as grounding context

---

## 4.5. Part 2: Open the Starter Code

1. In VS Code, navigate to `labs-dotnet/02-pv-agent/04-function-tool/Labfiles/`.

1. Open `Program.cs`. The file contains the complete PV Agent from D3-M3, including the `expense.budgetType` instruction. You will find two `TODO` comments marking where to add the tool.

1. Open `appsettings.json.example`, save a copy as `appsettings.json`, and fill in your Foundry project endpoint, model deployment name, and API key:

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "https://<your-foundry-project>.services.ai.azure.com/api/projects/<project>/",
        "DeploymentName": "gpt-4o-mini",
        "ApiKey": "your-api-key-here"
      }
    }
    ```

---

## 4.6. Part 3: Prepare the .NET Environment

1. Open the integrated terminal in VS Code (**Ctrl+\`** / **Cmd+\`**).

1. Navigate to the `Labfiles` folder and restore + build the project:

    **macOS / Linux:**
    ```bash
    cd labs-dotnet/02-pv-agent/04-function-tool/Labfiles
    dotnet restore
    dotnet build
    ```

    **Windows (PowerShell):**
    ```powershell
    cd labs-dotnet\02-pv-agent\04-function-tool\Labfiles
    dotnet restore
    dotnet build
    ```

    You should see **Build succeeded** with 0 errors before proceeding.

---

## 4.7. Part 4: Implement the `GetProjectBudget` Tool

1. In `Program.cs`, find the `TODO` comment that reads:

    ```csharp
    // TODO: Define the GetProjectBudget tool function here.
    ```

1. Replace the TODO comment block with the following function definition:

    ```csharp
    // Define the GetProjectBudget tool function
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

        // Parse header to find column indices
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
                return $"Project: {fields[nameIdx].Trim()}, Total Budget: {budget}, Remaining Budget: {remaining}";
            }
        }

        return $"Project '{projectName}' not found in the budget data.";
    }
    ```

    Notice how the function:
    - Uses `AppContext.BaseDirectory` to locate `data/projects_budget.csv` in the build output directory — the CSV is inside `Labfiles/data/` and is copied by the `<None Update>` entry in `PVAgent.csproj` (works identically on macOS and Windows)
    - Reads all lines and parses the header row dynamically — no hard-coded column indices
    - Does a case-insensitive match on the `project name` column
    - Returns a descriptive string that the agent reads and uses to populate `project.budgetSummary`

    > **Why is the CSV inside `Labfiles/data/`?** Each exercise is self-contained — the data file lives *inside* the project folder so `<None Update>` can locate it and copy it during `dotnet build`. This avoids platform-specific relative-path issues when running the same project on both macOS and Windows.

---

## 4.8. Part 5: Register the Tool with the Agent

1. Find the line that creates the `AIAgent` (the `AsAIAgent(...)` call).

1. Add `tools: [AIFunctionFactory.Create(GetProjectBudget)]` as a parameter:

    ```csharp
    // Create the AIAgent with the GetProjectBudget function tool registered
    AIAgent agent = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
        .GetChatClient(deploymentName)
        .AsAIAgent(
            instructions: pvAgentInstructions,
            name: "PVAgent",
            tools: [AIFunctionFactory.Create(GetProjectBudget)]);
    ```

    > **Note**: `AIFunctionFactory.Create` wraps the C# method as an `AIFunction`, extracting the `[Description]` attributes automatically to build the tool schema that the model uses.

---

## 4.9. Part 6: Update the Agent Instruction

The agent needs to know it has a tool available and what to do with the results. You'll add a rule to the `YOUR ROLE` section and update the output format.

1. In `pvAgentInstructions`, find the `// TODO` comment inside `YOUR ROLE` and replace it with this rule:

    ```
    - When the user provides a project name, call the GetProjectBudget tool to look up the project's budget and remaining budget. Use this data to populate project.budgetSummary in the output JSON.
    ```

1. In the `OUTPUT FORMAT` block, update the `project.budgetSummary` object to use `totalBudget` and `remainingBudget`:

    ```json
    "project": {
      "projectName": "...",
      "budgetSummary": {
        "totalBudget": 0,
        "remainingBudget": 0
      }
    },
    ```

1. Save the file (**Ctrl+S** / **Cmd+S**).

---

## 4.10. Part 7: Run the Agent

1. In the integrated terminal, run the project:

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

## 4.11. Part 8: Test with User Stories

Use the following scenarios to verify that the agent correctly calls `GetProjectBudget` and populates the budget data in the JSON output.

### User Story 1 — Monthly Subscription for a Known Project

```
I need to create a PV for our monthly GitHub Copilot subscription.
The cost is 5,000 baht per month. Project is IT Internal.
My name is Somchai Jaidee. Payee is GitHub Inc.
Purpose is to support developer productivity. Approver is Napat Ruangroj.
```

**Expected behavior:**
- Agent identifies `expense.type` as `MonthlyFee` and `expense.budgetType` as `"Expense"`
- Agent **automatically calls `GetProjectBudget`** with `"IT Internal"` as the argument
- Final JSON includes `project.budgetSummary.totalBudget` and `project.budgetSummary.remainingBudget` populated from the CSV

### User Story 2 — Equipment Purchase for Another Known Project

```
I want to submit a PV to purchase 5 ergonomic chairs for the HR team.
Total cost is 25,000 baht. This is for the HR Internal project.
My name is Wanchai Teeraphon. Payee is Office Furniture Co., Ltd.
Purpose is to improve workspace comfort. Approver is Manee Srisuk.
```

**Expected behavior:**
- Agent identifies `expense.type` as `OneTime` and `expense.budgetType` as `"Investment"`
- Agent calls `GetProjectBudget` with `"HR Internal"`
- Final JSON reflects the budget values from the CSV

### User Story 3 — Unknown Project Name

```
Please create a PV for a workshop registration fee, 3,500 baht.
Payee is Skillbridge Academy. My name is Chokchai Phongphan.
Project is Innovation Lab. Approver is Napat Ruangroj.
Purpose is to upskill the technology team.
```

**Expected behavior:**
- Agent calls `GetProjectBudget` with `"Innovation Lab"`
- The tool returns a "not found" message
- Agent reports that the project was not found in the budget data and may ask the user to verify the name
- Agent does **NOT** invent budget numbers

> **Observation question**: Compare the agent's behavior when the tool returns data (User Stories 1 & 2) vs. when it returns a "not found" message (User Story 3). How does the agent communicate the result to the user before proceeding?

---

## 4.12. Deliverables

After completing this exercise, you should have:

- ✅ Defined `GetProjectBudget` with `[Description]` attributes on the method and its parameter
- ✅ Used `AIFunctionFactory.Create(GetProjectBudget)` to register the tool in `AsAIAgent()`
- ✅ Updated `pvAgentInstructions` to instruct the agent to call the tool and populate `budgetSummary`
- ✅ Updated the `OUTPUT FORMAT` block to include `totalBudget` and `remainingBudget`
- ✅ Tested all three user stories and observed correct tool invocation behavior
