---
lab:
    title: 'D3-M3: PV Agent — Data Grounding via Agent Instruction (C#)'
    description: 'Learn how to ground agent output by encoding business classification logic directly into the agent instruction. Add expense.budgetType to the PV JSON output by tuning the existing instruction string in C#.'
    level: 200
    duration: 30
    islab: true
---

# 3. D3-M3: PV Agent — Data Grounding via Agent Instruction (C#)

In this exercise, you'll extend the PV Agent's output by adding a new field — `expense.budgetType` — **without changing any application code**. You'll achieve this purely by **tuning the agent instruction string** in C#, demonstrating how structured business rules can be grounded directly into an AI agent's behavior through the system prompt.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## 3.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Explain the concept of *data grounding via instruction* and when to use it
2. Identify the difference between agent instruction changes and code changes
3. Add new classification logic to an existing agent instruction string in C#
4. Define enum-driven field rules that the agent can reason about from user context
5. Test the updated output with user story scenarios

## 3.2. Prerequisites

- Completed **D3-M2** (PV Agent instruction defined, multi-turn conversation loop working, `appsettings.json` configured)
- .NET 8.0 SDK installed and `dotnet build` succeeded in D3-M2

## 3.3. Scenario

The back-office finance team has a new requirement: every Payment Voucher must now include a **budget type** field to distinguish between operational costs and capital investments. The field is `expense.budgetType` and must be exactly `"Expense"` or `"Investment"`.

The business classification rules are:

| budgetType | When to use |
|---|---|
| `Expense` | Recurring or operational costs — subscriptions, monthly fees, training registrations, consumable items |
| `Investment` | One-time capital expenditures that create lasting value — equipment purchases, tool licenses, infrastructure upgrades |

Because this is a **classification rule based on context**, it is best grounded directly in the agent instruction rather than implemented as code logic. The agent already understands the user's intent from the conversation — you just need to give it the rule.

---

## 3.4. Part 1: Understand Data Grounding via Instruction

### What is data grounding via instruction?

*Data grounding* is the practice of providing the agent with the knowledge and rules it needs to produce accurate, consistent output — before the user says anything. When done via instruction (system prompt), it means encoding:

- **Enum definitions**: the exact allowed values and what each means
- **Classification logic**: how to map user-provided context to a specific value
- **Derivation rules**: how to infer a field value from other information already collected

In this case, the agent already knows:
- The user's described purpose (e.g., "monthly subscription", "equipment purchase")
- The expense type (`MonthlyFee` or `OneTime`)

You will add a rule that lets the agent derive `expense.budgetType` from this existing context.

### Why not use code?

You *could* write a C# method that maps keywords to budget types. But that approach:
- Requires maintaining a keyword list that may miss edge cases
- Cannot handle nuanced descriptions like *"a one-time upgrade to our monitoring tool"*
- Adds code complexity for purely semantic classification

The agent's language understanding is better suited for this task. The instruction string is the right place to ground this rule.

---

## 3.5. Part 2: Test the Current Behavior in the Foundry Portal

Before changing the instruction, observe the current behavior without `budgetType`.

1. Open the [Foundry portal](https://ai.azure.com) and navigate to your `[username]-pv-agent` project.

1. In the left navigation, select **Build** > **Agents** and open your `pv-agent`.

1. Start a **new chat session** and enter:

    ```
    I need to create a PV for our monthly GitHub Copilot subscription.
    The cost is 5,000 baht per month. Project is IT Internal.
    My name is Somchai Jaidee and the approver is Napat Ruangroj.
    The payee is GitHub Inc. Purpose is to support developer productivity tools.
    ```

1. Observe the final JSON output — notice that `expense.budgetType` **does not appear** because the instruction does not mention it.

---

## 3.6. Part 3: Update the Agent Instruction in C#

This is the only file you need to change — `Program.cs`. **No other code needs to be modified.**

### 3.6.1. Prepare the .NET project

1. In VS Code, navigate to `labs-dotnet/02-pv-agent/03-data-grounding/Labfiles/`.

1. Right-click the folder and select **Open in Integrated Terminal**.

1. Run the following commands to restore NuGet packages and build the project:

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

1. Copy `appsettings.json.example` to `appsettings.json` and fill in your values:

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "<your-project-endpoint>",
        "DeploymentName": "gpt-4.1",
        "ApiKey": "<your-api-key>"
      }
    }
    ```

1. Open `Program.cs` and find the `pvAgentInstructions` string. You will see two `[TODO]` comments marking exactly where to make changes.

### 3.6.2. Update the REQUIRED FIELDS section

1. In `Program.cs`, find the `REQUIRED FIELDS` block inside `pvAgentInstructions`.

1. Locate the `[TODO: add expense.budgetType rule here]` placeholder and **replace it** with:

    ```
    - expense.budgetType: MUST be exactly "Expense" or "Investment" — derive this from context:
        - Use "Expense" for recurring or operational costs (subscriptions, monthly fees, training, consumables)
        - Use "Investment" for one-time capital expenditures that create lasting value (equipment, tool licenses, infrastructure)
        - If the user's intent is ambiguous, infer from expense.type: MonthlyFee → lean toward "Expense", OneTime → consider both and ask if still unclear
    ```

### 3.6.3. Update the OUTPUT FORMAT section

1. In the same `pvAgentInstructions` string, find the `OUTPUT FORMAT` block and locate the `expense` object.

1. Find the `[TODO: add budgetType field here]` placeholder and **replace it** with:

    ```
    "budgetType": "Expense | Investment",
    ```

    The updated expense block should look like this:

    ```json
    "expense": {
      "type": "MonthlyFee | OneTime",
      "budgetType": "Expense | Investment",
      "amount": { "value": 0, "currency": "THB" }
    },
    ```

1. Save the file (**Ctrl+S** / **Cmd+S**).

---

## 3.7. Part 4: Run and Test the Updated Agent

### Run the agent

1. In the terminal, run:

    **macOS / Linux / Windows:**
    ```bash
    dotnet run
    ```

1. The console will show the PV Agent prompt. Test using the user stories below.

### User Story 1 — Monthly Software Subscription (Expense)

Enter the following in the agent console:

```
I need to create a PV for our monthly GitHub Copilot subscription.
The cost is 5,000 baht per month. Project is IT Internal.
My name is Somchai Jaidee and the approver is Napat Ruangroj.
The payee is GitHub Inc. Purpose is to support developer productivity tools.
```

**Expected behavior:**
- Agent identifies `expense.type` as `MonthlyFee`
- Agent derives `expense.budgetType` as `"Expense"` (recurring subscription)
- Final JSON includes `"budgetType": "Expense"` inside the `expense` block
- Agent does NOT set `ReadyForSubmission` until user confirms

### User Story 2 — Equipment Purchase (Investment)

Start a **new session** (type `quit` and run again), then enter:

```
I want to submit a PV to purchase 10 USB-C docking stations for the engineering team.
Total cost is 45,000 baht. Project is Office Upgrade FY26.
My name is Wanchai Teeraphon. Payee is TechGear Co., Ltd.
The approver is Manee Srisuk. Purpose is to improve remote work setup.
```

**Expected behavior:**
- Agent identifies `expense.type` as `OneTime`
- Agent derives `expense.budgetType` as `"Investment"` (capital equipment)
- Final JSON includes `"budgetType": "Investment"` inside the `expense` block

### User Story 3 — Ambiguous One-Time Cost (Agent infers or asks)

Start a **new session**, then enter:

```
Please create a PV for a workshop registration fee, 3,500 baht.
Payee is Skillbridge Academy. My name is Chokchai Phongphan.
Project is HR Development. Approver is Napat Ruangroj.
```

**Expected behavior:**
- Agent identifies `expense.type` as `OneTime`
- Because a training registration fee is operationally classified, agent should derive `"Expense"`
- If the agent is unsure, it may ask the user to clarify — this is acceptable behavior
- Final JSON includes `"budgetType"` set to either `"Expense"` or the value the user confirmed

---

## 3.8. Summary

In this exercise, you:

- Added `expense.budgetType` to the PV Agent's output by updating only the **instruction string** — no code logic was needed
- Defined an enum-driven classification rule with contextual fallback logic
- Validated the agent's behavior with three user stories covering clear and ambiguous cases

The key insight: **the agent instruction is part of your application design**. Grounding semantic classification rules in the instruction produces more robust behavior than keyword-matching in code.

> **Next step**: In **D3-M4**, you'll add a C# function tool to the PV Agent that allows it to look up real project budget data from a CSV file at runtime.
