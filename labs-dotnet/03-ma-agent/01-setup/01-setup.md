---
lab:
    title: 'D4-M1: MA Agent — Setup & Agent Foundation (C#)'
    description: 'Create a Manager Assistant (MA) Agent in C# that helps managers review and explore Payment Voucher requests. Design the agent instruction, implement the conversation loop using the Microsoft Agent Framework, and test with realistic manager scenarios.'
    level: 200
    duration: 45
    islab: true
---

# 1. D4-M1: MA Agent — Setup & Agent Foundation (C#)

In this exercise, you'll create a **Manager Assistant (MA) Agent** — a companion agent to the PV Agent you built in the previous track. While the PV Agent helps requestors *create* payment vouchers, the MA Agent helps managers *review and explore* those vouchers shared in the conversation.

This exercise focuses on setting up the MA Agent, designing its system instruction, and testing it with sample PV data in a streaming multi-turn conversation loop.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## 1.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Describe the MA Agent's role and how it complements the PV Agent
2. Write an agent instruction tailored to analyzing structured PV JSON data
3. Implement the MA Agent conversation loop using the **Microsoft Agent Framework** in C#
4. Use `AgentSession` to maintain multi-turn conversation history
5. Test the agent with realistic manager review scenarios directly in the terminal

## 1.2. Prerequisites

- Completed **D3** (PV Agent track): Foundry project created, GPT-4.1 deployed
- Microsoft Foundry project **Endpoint** and **API key** (used in previous D3 exercises)
- .NET 8 SDK installed
- Microsoft Foundry VS Code extension installed and connected

## 1.3. Scenario

Your organization has deployed the PV Agent for requestors to submit payment voucher requests. As a manager, you need a way to quickly understand what's pending, spot incomplete entries, and ask follow-up questions about specific requests.

The MA Agent acts as your **intelligent review assistant** — you share PV data with it, and it helps you make sense of what's there.

**What the MA Agent DOES:**
- Summarizes PV requests shared in the conversation
- Identifies missing fields and flags incomplete entries
- Answers questions about PV content and highlights key details
- Compares multiple PV entries when asked

**What the MA Agent does NOT do:**
- Approve or reject vouchers
- Modify PV records
- Create new PV requests
- Invent or assume data not present in what you share

---

## Part 1: Understand the MA Agent Architecture

Before writing any code, understand how the MA Agent fits into the overall system.

### MA Agent Position in the Architecture

```
PV Agent (D3)                      MA Agent (D4)
    ↓  collects & submits PV            ↑  reads & analyzes PV
Cosmos DB  ──────────────────────── Cosmos DB
(payment-vouchers container)        (same container)
```

The MA Agent and PV Agent share the same Cosmos DB container (`payment-vouchers`). The PV Agent writes new documents; the MA Agent reads and interprets them.

### PV Data Contract

The MA Agent must understand the PV JSON structure produced by the PV Agent:

```json
{
  "pv": {
    "pvTitle": "Short descriptive title",
    "requestDate": "YYYY-MM-DD",
    "requestor": { "name": "Name of the requestor" },
    "payee": { "name": "Name or company receiving payment" },
    "purpose": {
      "for": "What is being paid for",
      "objective": "Business reason or objective"
    },
    "expense": {
      "type": "MonthlyFee | OneTime",
      "amount": { "value": 0, "currency": "THB" }
    },
    "project": {
      "projectName": "Project name",
      "budgetSummary": {
        "totalBudget": 0,
        "remainingBudget": 0
      }
    },
    "approval": { "approverName": "...", "status": "Pending | Approved" },
    "status": "Draft | ReadyForSubmission"
  }
}
```

> **Key values to remember:**
> - `expense.type`: `MonthlyFee` (recurring) or `OneTime` (single payment)
> - `approval.status`: `Pending` (awaiting review) or `Approved` (already processed)
> - `status`: `Draft` (incomplete, not submitted) or `ReadyForSubmission` (complete, ready for manager review)

---

## Part 2: Configure the Project

### Set up the .NET project

1. In VS Code, navigate to `labs-dotnet/03-ma-agent/01-setup/Labfiles/`.

1. Right-click the **Labfiles** folder and select **Open in Integrated Terminal**.

1. Restore and build the project by running the following commands:

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

    **Windows (Command Prompt):**
    ```cmd
    dotnet restore
    dotnet build
    ```

### Set up appsettings.json

1. In the `Labfiles/` folder, copy `appsettings.json.example` to a new file named `appsettings.json`.

1. Fill in your values:

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "https://your-project-endpoint/openai/v1",
        "DeploymentName": "gpt-4.1",
        "ApiKey": "your-api-key"
      }
    }
    ```

    To get your **Endpoint** and **ApiKey**:
    - In the Foundry extension sidebar, right-click your project name and select **Copy Project Endpoint**.
    - For the API key, go to **Microsoft Foundry portal** → your project → **Overview** → **Keys**.

    > **Important**: `appsettings.json` is listed in `.gitignore` — it will not be committed to source control.

---

## Part 3: Implement the MA Agent

Open `Program.cs` in `Labfiles/`. You'll complete three TODO blocks.

### Step 1 — Define the MA Agent instruction

Find the `[TODO 1]` comment and replace the placeholder string with the following:

```csharp
// [TODO 1] Define MA Agent instructions
string maAgentInstructions = """
    You are "MA Agent" — an AI assistant that helps managers review and explore Payment Voucher (PV) requests.

    YOUR ROLE:
    - Help managers understand and analyze PV request details shared in the conversation
    - Summarize PV data clearly and concisely
    - Identify missing fields, potential issues, or anomalies in PV data
    - Answer questions about PV content based on the data provided
    - Highlight important information such as expense amounts, requestors, and approval status
    - Compare multiple PV entries when asked

    UNDERSTANDING PV DATA:
    The PV data follows this JSON structure:
    {
      "pv": {
        "pvTitle": "Short descriptive title",
        "requestDate": "YYYY-MM-DD",
        "requestor": { "name": "Name of the requestor" },
        "payee": { "name": "Name or company receiving payment" },
        "purpose": {
          "for": "What is being paid for",
          "objective": "Business reason or objective"
        },
        "expense": {
          "type": "MonthlyFee | OneTime",
          "amount": { "value": 0, "currency": "THB" }
        },
        "project": {
          "projectName": "Project name",
          "budgetSummary": {
            "totalBudget": 0,
            "remainingBudget": 0
          }
        },
        "approval": { "approverName": "Name of approver", "status": "Pending | Approved" },
        "status": "Draft | ReadyForSubmission"
      }
    }

    KEY FIELD INTERPRETATIONS:
    - expense.type "MonthlyFee": Recurring monthly payment
    - expense.type "OneTime": Single one-time payment
    - approval.status "Pending": Awaiting manager approval
    - approval.status "Approved": Already approved
    - status "Draft": Incomplete or not yet submitted
    - status "ReadyForSubmission": Complete and ready for manager review

    CONSTRAINTS:
    - You do NOT approve or reject PV requests
    - You do NOT modify PV data
    - You do NOT create new PV requests
    - If PV data is incomplete, note which fields are missing but do not fill them in
    - Always base your analysis on the exact data provided — do not invent or assume unreported values
    """;
```

### Step 2 — Create the AIAgent

Find the `[TODO 2]` comment and replace the `null!` assignment:

```csharp
// [TODO 2] Create the AIAgent using OpenAI-compatible client and MA Agent instructions
AIAgent agent = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: maAgentInstructions,
        name: "MAAgent");
```

### Step 3 — Create the AgentSession

Find the `[TODO 3]` comment and replace the `null!` assignment:

```csharp
// [TODO 3] Create an AgentSession to maintain conversation history across turns
AgentSession session = await agent.CreateSessionAsync();
```

`AgentSession` automatically maintains conversation history across all turns — you don't need to manage a history list manually.

1. Save the file (**Ctrl+S** / **Cmd+S**).

### Run the agent

1. In the terminal (inside `Labfiles/`), run:

    ```bash
    dotnet run
    ```

1. When the agent starts, you should see:

    ```
    MA Agent - Manager Assistant
    ========================================
    MA Agent is ready. Paste a PV JSON or ask a question. Type 'quit' to exit.
    ```

1. The agent is now running. Continue to **Part 4** to test it with manager scenarios.

---

## Part 4: Test with Manager Scenarios

Test your MA Agent using two realistic scenarios. Paste the prompts below directly into the running agent terminal.

### Scenario 1 — Review a Single PV

At the `You:` prompt, enter the following:

```
Here is a pending PV request submitted by one of my team members. Can you summarize it and tell me if anything looks concerning?

{"pv": {"pvTitle": "Monthly GitHub Copilot Subscription", "requestDate": "2026-03-01", "requestor": {"name": "Somchai Jaidee"}, "payee": {"name": "GitHub Inc."}, "purpose": {"for": "Monthly developer tool subscription", "objective": "Improve developer productivity for IT Internal project"}, "expense": {"type": "MonthlyFee", "amount": {"value": 5000, "currency": "THB"}}, "project": {"projectName": "IT Internal", "budgetSummary": {"totalBudget": 500000, "remainingBudget": 380000}}, "approval": {"approverName": "Napat Ruangroj", "status": "Pending"}, "status": "ReadyForSubmission"}}
```

**Expected behavior:**
- Agent provides a clear, readable summary of the PV
- Agent notes that `status` is `ReadyForSubmission` — this is ready for your review
- Agent identifies no critical issues since all fields are present
- Agent does NOT approve or modify the request

### Scenario 2 — Compare Two PVs and Identify Issues

Type `quit` to exit the current session, then run `dotnet run` again for a fresh conversation. At the `You:` prompt, enter:

```
I have two PV requests to review. Which one is ready for approval and which needs more information?

PV 1: {"pv": {"pvTitle": "Conference Registration Fee", "requestDate": "2026-03-15", "requestor": {"name": "Prasert Kaewkla"}, "payee": {"name": "TechConf Thailand"}, "purpose": {"for": "Annual developer conference registration", "objective": "Team learning and networking"}, "expense": {"type": "OneTime", "amount": {"value": 3500, "currency": "THB"}}, "project": {"projectName": "HR Development", "budgetSummary": {"totalBudget": 200000, "remainingBudget": 150000}}, "approval": {"approverName": "Manee Srisuk", "status": "Pending"}, "status": "ReadyForSubmission"}}

PV 2: {"pv": {"pvTitle": "USB-C Hub Purchase", "requestDate": "2026-03-20", "requestor": {"name": "Chokchai Phongphan"}, "payee": {"name": ""}, "purpose": {"for": "Hardware purchase", "objective": ""}, "expense": {"type": "OneTime", "amount": {"value": 1200, "currency": "THB"}}, "project": {"projectName": "", "budgetSummary": {"totalBudget": 0, "remainingBudget": 0}}, "approval": {"approverName": "", "status": "Pending"}, "status": "Draft"}}
```

**Expected behavior:**
- Agent identifies PV 1 as complete and `ReadyForSubmission`
- Agent identifies PV 2 as `Draft` with multiple missing fields: `payee.name`, `purpose.objective`, `project.projectName`, and `approval.approverName`
- Agent clearly lists what is missing in PV 2 without inventing or filling in the gaps
- Agent does NOT approve either request

> **Observation**: Does the agent correctly distinguish between a complete and an incomplete PV? Does it avoid making up missing information? If not, revisit the `maAgentInstructions` in `Program.cs` and rerun.

1. When you're satisfied with both scenarios, type `quit` to exit.

---

## Deliverables

After completing this exercise, you should have:

- ✅ A working `Program.cs` that creates and runs the MA Agent via the Microsoft Agent Framework
- ✅ `AgentSession` maintaining multi-turn conversation history automatically
- ✅ Tested two manager review scenarios: a complete PV and a comparison exposing missing fields
- ✅ Confirmed the agent identifies complete vs. incomplete PV entries without inventing data

---

## Summary

In this exercise, you:

- Described the MA Agent's role as a manager-facing companion to the PV Agent
- Designed an agent instruction that understands and interprets PV JSON data
- Implemented the MA Agent conversation loop in C# using `OpenAIClient`, `AsAIAgent()`, and `AgentSession` from the Microsoft Agent Framework
- Streamed agent responses token-by-token with `RunStreamingAsync`
- Tested two scenarios: a clean PV review and a comparison that exposed missing fields

In the next module (**D4-M3**), you'll add **Cosmos DB function tools** so the MA Agent can retrieve PV records directly from the database instead of relying on manually pasted data.
