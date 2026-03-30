---
lab:
    title: 'D3-M3: PV Agent — Data Grounding via Agent Instruction'
    description: 'Learn how to ground agent output by encoding business classification logic directly into the agent instruction. Add expense.budgetType to the PV JSON output by tuning the existing instruction.'
    level: 200
    duration: 30
    islab: true
---

# D3-M3: PV Agent — Data Grounding via Agent Instruction

In this exercise, you'll extend the PV Agent's output by adding a new field — `expense.budgetType` — without changing any application code. You'll achieve this purely by **tuning the agent instruction**, demonstrating how structured business rules can be grounded directly into an AI agent's behavior.

This exercise takes approximately **30** minutes.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## Learning Objectives

By the end of this exercise, you'll be able to:

1. Explain the concept of *data grounding via instruction* and when to use it
2. Identify the difference between agent instruction changes and code changes
3. Add new classification logic to an existing agent instruction
4. Define enum-driven field rules that the agent can reason about from context
5. Test the updated output with user story scenarios

## Prerequisites

- Completed **D3-M2** (PV Agent instruction defined, agent running in Python, `.env` configured)
- Microsoft Foundry VS Code extension installed and connected
- Python virtual environment activated with packages installed

## Scenario

The back-office finance team has a new requirement: every Payment Voucher must now include a **budget type** field to distinguish between operational costs and capital investments. Specifically, the field is `expense.budgetType` and must be exactly `"Expense"` or `"Investment"`.

The business classification rules are:

| budgetType | When to use |
|---|---|
| `Expense` | Recurring or operational costs — subscriptions, monthly fees, training registrations, consumable items |
| `Investment` | One-time capital expenditures that create lasting value — equipment purchases, tool licenses, infrastructure upgrades |

Because this is a **classification rule based on context**, it is best grounded directly in the agent instruction rather than implemented as code logic. The agent already understands the user's intent from the conversation — you just need to give it the rule.

---

## Part 1: Understand Data Grounding via Instruction

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

You *could* write a Python function that maps keywords to budget types. But that approach:
- Requires maintaining a keyword list that may miss edge cases
- Cannot handle nuanced descriptions like *"a one-time upgrade to our monitoring tool"*
- Adds code complexity for purely semantic classification

The agent's language understanding is better suited for this classification. The instruction is the right place to ground this rule.

---



## Part 2: Update the Agent Instruction in Python

Now that you've validated the instruction change in the portal, apply the same update to your Python file.

### Open the Python file

1. In VS Code, navigate to `labs/02-pv-agent/03-data-grounding/Labfiles/`.

1. Open `pv_agent.py`. The file contains the agent from D3-M2 with the original instruction.

1. Open `.env.example`, save a copy as `.env`, and fill in your project endpoint and model name.

### Update the REQUIRED FIELDS section

1. Find the `REQUIRED FIELDS` block inside `PV_AGENT_INSTRUCTIONS`.

1. After the `expense.amount.currency` line, add the `budgetType` rule:

    ```python
    - expense.budgetType: MUST be exactly "Expense" or "Investment" — derive this from context:
        - Use "Expense" for recurring or operational costs (subscriptions, monthly fees, training, consumables)
        - Use "Investment" for one-time capital expenditures that create lasting value (equipment, tool licenses, infrastructure)
        - If the user's intent is ambiguous, infer from expense.type: MonthlyFee → lean toward "Expense", OneTime → consider both and ask if still unclear
    ```

### Update the OUTPUT FORMAT section

1. Find the `OUTPUT FORMAT` block and locate the `expense` object.

1. Add `"budgetType"` to the expense block so it reads:

    ```python
    "expense": {
        "type": "MonthlyFee | OneTime",
        "budgetType": "Expense | Investment",
        "amount": { "value": 0, "currency": "THB" }
    },
    ```

1. Save the file (**Ctrl+S**).



## Part 3: Run the agent

1. In the terminal (with your virtual environment active), run:

    ```bash
    python pv_agent.py
    ```

1. Test using User Stories 1 and 2 from Part 3.


Test the updated instruction using three user stories. Observe whether the agent correctly derives `expense.budgetType` in each case.

### User Story 1 — Monthly Software Subscription (Expense)

Start a **new chat session** in the playground, then enter:

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

Start a **new chat session**, then enter:

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

### User Story 3 — Ambiguous One-Time Cost (Agent asks or infers)

Start a **new chat session**, then enter:

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

> **Observation question**: Does the agent correctly derive `budgetType` without being explicitly told by the user? In which cases does it ask for clarification? Note your observations — instruction tuning is iterative.

---

2. Confirm the final JSON output contains `expense.budgetType` with the correct value.

3. Type `quit` to exit.

---

## Deliverables

After completing this exercise, you should have:

- ✅ Updated agent instruction in the Foundry portal with `expense.budgetType` rule
- ✅ Updated `pv_agent.py` instruction with `expense.budgetType` rule
- ✅ Tested all three user stories and observed correct `budgetType` derivation
- ✅ Notes on when the agent derives the value vs. asks the user

---

## Summary

In this exercise, you:

- Learned when to use *data grounding via instruction* instead of code
- Extended the PV Agent output with a new classified field `expense.budgetType`
- Encoded enum values and classification logic directly into the agent instruction
- Validated the change with three user stories covering `Expense`, `Investment`, and an ambiguous case

In the next module (**D3-M4**), you'll add a **custom tool** to enable the agent to look up project budget data at runtime.
