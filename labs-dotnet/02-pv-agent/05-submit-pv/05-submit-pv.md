---
lab:
    title: 'D4-M1: PV Agent — Submit PV with a Second Function Tool (C#)'
    description: 'Add a SubmitPv function tool that receives the completed PV JSON and prints it as a placeholder for Cosmos DB persistence. Tune the agent instruction to gate the tool call behind data completeness checks.'
    level: 200
    duration: 30
    islab: true
---

# 5. D4-M1: PV Agent — Submit PV with a Second Function Tool (C#)

In this exercise, you'll add a second function tool — `SubmitPv` — to the PV Agent. When the user confirms a completed Payment Voucher, the agent calls this tool and passes the full PV JSON object to it. The tool prints the JSON to the terminal as a placeholder for actual Cosmos DB insertion, which you'll implement in the next exercise.

You'll also tune the agent instruction so the agent **never calls `SubmitPv` until every required field is filled** — ensuring the data arriving at the tool is always complete and insertion-ready.

This exercise takes approximately **30** minutes.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## 5.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Define a second `AIFunctionFactory` tool that accepts a structured JSON string as input
2. Use `[Description]` attributes on the method and parameter to gate the agent's tool call
3. Tune the agent instruction with completeness and trigger rules for the submission workflow
4. Verify the agent hands off the right payload to the tool by reading the terminal output
5. Understand the pattern of separating data collection from persistence

## 5.2. Prerequisites

- Completed **D3-M4** (PV Agent running with `GetAllProjectBudgets` tool, `appsettings.json` configured)
- .NET 8.0 SDK installed and `dotnet build` succeeded in D3-M4

## 5.3. Scenario

The finance team is ready to store Payment Vouchers in Azure Cosmos DB. Before writing the database code, you need to:

1. Define the tool interface the agent will call
2. Tune the agent instruction so the agent always passes a **complete, valid** PV JSON — no missing fields, no `"Draft"` status
3. Validate the integration end-to-end by inspecting the JSON printed to the terminal

This pattern — stubbing the persistence layer first and validating the payload shape — is standard practice before wiring up a cloud database.

---

## Part 1: Understand the Two-Tool Pattern

### Data flow direction

In D3-M4 you built a tool that *returns* data to the agent (budget lookup). In this exercise you'll build a tool that *receives* data from the agent — the completed PV JSON.

The data flow is reversed:

```
User confirms PV
    → Agent assembles full JSON
        → Agent calls SubmitPv(pvJson="{ ... }")
            → Tool receives the string, prints it to the terminal
                → Tool returns a confirmation string to the agent
```

The parameter type is `string` (a JSON-encoded string). The `[Description]` attribute on the parameter tells the agent exactly what to put in that string.

### Gating the tool call with instruction rules

Without explicit rules, the agent might call `SubmitPv` too early — before all fields are collected — or forget to call it after confirmation. You'll add two rules to `pvAgentInstructions`:

1. **Completeness gate** — only call `SubmitPv` when `status` is `"ReadyForSubmission"` and all required fields are non-empty real values
2. **Trigger rule** — call `SubmitPv` immediately after the user confirms the summary

Both rules belong in the agent instruction **and** in the `[Description]` attribute on the method — the method description is the first thing the LLM reads when deciding whether to invoke the tool.

---

## Part 2: Open the Starter Code

1. In VS Code, navigate to `labs-dotnet/02-pv-agent/05-submit-pv/Labfiles/`.

1. Open `appsettings.json.example`. Copy the file and rename it `appsettings.json`:

    **macOS / Linux:**
    ```bash
    cp appsettings.json.example appsettings.json
    ```

    **Windows (PowerShell):**
    ```powershell
    Copy-Item appsettings.json.example appsettings.json
    ```

1. Open `appsettings.json` and fill in your Foundry project values:

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "<your Foundry project endpoint>",
        "DeploymentName": "<your model deployment name>",
        "ApiKey": "<your Foundry project API key>"
      }
    }
    ```

    > **Tip**: These are the same values you used in D3-M4. You can copy them from that exercise's `appsettings.json`.

1. Open `Program.cs`. The file contains the complete PV Agent from D3-M4, including the `GetAllProjectBudgets` tool. This is your starting point. There are **three `// TODO` blocks** for you to complete.

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

    You should see `Build succeeded` with 0 errors before continuing.

---

## Part 4: Add the SubmitPv Instruction Rules

The agent instruction controls **when** the agent decides to call `SubmitPv`. Without explicit rules, the agent may call it too early or not at all.

1. In `Program.cs`, find the `// TODO` block inside `pvAgentInstructions`:

    ```csharp
    // TODO: Add two SubmitPv gating rules here:
    //   1. Completeness gate — do NOT call SubmitPv until all required fields are filled
    //      and status is "ReadyForSubmission" (no placeholders, no zeros for required amounts)
    //   2. Trigger rule — call SubmitPv immediately after the user confirms the summary
    ```

1. Replace the `// TODO` comment with the following two rules:

    ```
    - Do NOT call SubmitPv until the user has confirmed the summary and ALL required fields are filled with real values (no placeholders, no empty strings, no zeros for required amounts).
    - when user request to submit the pv, submit PV data to system.
    ```

2. Save the file.

---

## Part 5: Implement the `SubmitPv` Tool Function

1. Find the `// TODO: Define the SubmitPv tool function here` comment in `Program.cs`, located after the `GetAllProjectBudgets` function.

1. Add the following function below that comment:

    ```csharp
    [Description("submit pv data to system for approval")]
    async Task<string> SubmitPv(
        [Description("The complete PV JSON object as a string")]
        string pvJson, CancellationToken ct = default)
    {
        Console.WriteLine("\n--- PV Submission Received ---");
        Console.WriteLine(pvJson);
        Console.WriteLine("--- End of PV Submission ---\n");
        return "PV submission received and ready for database insertion.";
    }
    ```

    Key points:
    - The `[Description]` on the **method** tells the agent *when* to call it — "after confirmation", "ReadyForSubmission" — this is what the LLM reads to decide whether to invoke the tool
    - The `[Description]` on the **parameter** tells the agent *what to put in it* — the complete PV JSON with all fields filled
    - The method body prints the JSON between separator lines and returns a confirmation string the agent will relay to the user
    - No Cosmos DB code yet — that comes in the next exercise

1. Save the file.

---

## Part 6: Register `SubmitPv` with the Agent

1. Find the `// TODO: add AIFunctionFactory.Create(SubmitPv) to the tools list` comment in the `AsAIAgent(...)` call.

1. Add `AIFunctionFactory.Create(SubmitPv)` to the `tools` array alongside the existing `GetAllProjectBudgets` tool:

    ```csharp
    tools: [AIFunctionFactory.Create(GetAllProjectBudgets), AIFunctionFactory.Create(SubmitPv)]
    ```

1. Save the file.

---

## Part 7: Run the Agent

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

## Part 8: Test with User Stories

### User Story 1 — Full Data, Immediate Submission

```
I need to create a PV for our monthly GitHub Copilot subscription.
The cost is 5,000 baht per month. Project is IT Internal.
My name is Somchai Jaidee. Payee is GitHub Inc.
Purpose is to support developer productivity tools. Approver is Napat Ruangroj.
```

**Expected behavior:**
1. Agent asks any missing fields, then shows a confirmation summary
2. You type **"yes"** (or confirm)
3. The terminal prints:
    ```
    --- PV Submission Received ---
    { "pv": { "pvTitle": "...", ... "status": "ReadyForSubmission" } }
    --- End of PV Submission ---
    ```
4. The agent replies: *"PV submission received and ready for database insertion."*

> **Observe**: Is `status` set to `"ReadyForSubmission"`? Are `totalBudget` and `remainingBudget` populated with real numbers from the CSV?

### User Story 2 — One-Time Equipment Purchase

```
I want to submit a PV to purchase 5 ergonomic chairs for the HR team.
Total cost is 25,000 baht. This is for the HR Internal project.
My name is Wanchai Teeraphon. Payee is Office Furniture Co., Ltd.
Purpose is to improve workspace comfort. Approver is Manee Srisuk.
```

**Expected behavior:**
- Agent derives `expense.type` as `OneTime` and `expense.budgetType` as `"Investment"`
- After confirmation, `SubmitPv` is called and the JSON is printed to the terminal

### User Story 3 — Partial Data, Agent Gates the Submission

```
Please create a PV for a training workshop, 3,500 baht.
```

**Expected behavior:**
- Agent asks for all missing fields one at a time
- Agent does **not** call `SubmitPv` until you have confirmed the completed summary
- After confirmation, the terminal prints the full PV JSON

> **Observe**: If you say "submit" before answering all the questions, does the agent refuse and keep asking? The instruction rules you added in Part 4 control this behavior.

---

## Deliverables

After completing this exercise, you should have:

- ✅ Two instruction rules added to `pvAgentInstructions` (completeness gate + trigger rule)
- ✅ `SubmitPv` function defined with `[Description]` on method and parameter
- ✅ Both `GetAllProjectBudgets` and `SubmitPv` registered via `AIFunctionFactory.Create` in `tools:`
- ✅ `dotnet run` succeeds and the agent prints the PV JSON to the terminal after user confirmation
- ✅ The printed JSON has `"status": "ReadyForSubmission"` and real `totalBudget` / `remainingBudget` values
