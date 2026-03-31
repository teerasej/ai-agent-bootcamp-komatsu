---
lab:
    title: 'D4-M1: MA Agent — Manager Assistant Setup & Agent Foundation'
    description: 'Create a Manager Assistant (MA) Agent that helps managers review and explore Payment Voucher requests stored in Cosmos DB. Design the agent instruction, implement the conversation loop, and test with realistic manager scenarios.'
    level: 200
    duration: 45
    islab: true
---

# D4-M1: MA Agent — Manager Assistant Setup & Agent Foundation

In this exercise, you'll create a **Manager Assistant (MA) Agent** — a companion agent to the PV Agent you built in the previous track. While the PV Agent helps requestors *create* payment vouchers, the MA Agent helps managers *review and explore* those vouchers stored in Azure Cosmos DB.

This exercise focuses on setting up the MA Agent, designing its instruction, and testing it with sample PV data.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## Learning Objectives

By the end of this exercise, you'll be able to:

1. Describe the MA Agent's role and how it complements the PV Agent
2. Write an agent instruction tailored to analyzing structured PV JSON data
3. Implement the MA Agent conversation loop using the Microsoft Agent Framework SDK
4. Test the agent with realistic manager review scenarios directly in the terminal

## Prerequisites

- Completed **D3** (PV Agent track): Foundry project created, GPT-4.1 deployed, `.env` configured
- Azure Cosmos DB instance provisioned from **D3-M6** with PV data stored in the `payment-vouchers` container
- Microsoft Foundry VS Code extension installed and connected
- Python 3.11 or later installed

## Scenario

Your organization has deployed the PV Agent for requestors to submit payment voucher requests. Those requests are now accumulating in Azure Cosmos DB with a `status` of `ReadyForSubmission`. As a manager, you need a way to quickly understand what's pending, spot incomplete entries, and ask follow-up questions about specific requests.

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

Before writing any code, take a moment to understand how the MA Agent fits into the overall system.

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
  "pv": 
  {
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
        "monthlyBudget": 0
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

## Part 2: Implement the MA Agent in Python

### Configure the Python virtual environment

1. In VS Code, navigate to the `labs/03-ma-agent/01-setup/Labfiles/` folder.

1. Right-click the folder and select **Open in Integrated Terminal**.

1. In the integrated terminal, run the following commands to create and activate a virtual environment, then install the required packages:

    **macOS / Linux:**
    ```bash
    python -m venv labenv
    source labenv/bin/activate
    pip install -r requirements.txt
    ```

    **Windows (PowerShell):**
    ```powershell
    python -m venv labenv
    .\labenv\Scripts\Activate.ps1
    pip install -r requirements.txt
    ```

### Open the starter code

1. In VS Code, open the `ma_agent.py` file in the `Labfiles/` folder. Review the existing structure — it has placeholder comments to guide you.

1. Open the `.env.example` file, save a copy as `.env` in the same folder, and fill in your project endpoint and model name:

    ```env
    PROJECT_ENDPOINT=<your-project-endpoint>
    MODEL_DEPLOYMENT_NAME=gpt-4.1
    ```

    To get your **PROJECT_ENDPOINT**:
    - In the Foundry extension sidebar, right-click your project name and select **Copy Project Endpoint**.
    - Paste the copied URL as the value for `PROJECT_ENDPOINT`.

### Add references

1. At the top of `ma_agent.py`, find the comment `# Add references` and add the following imports:

    ```python
    # Add references
    from agent_framework import Agent
    from agent_framework.azure import AzureOpenAIResponsesClient
    from azure.identity import AzureCliCredential
    ```

### Add the MA Agent instruction

1. Find the comment `# Define MA Agent instructions` and replace the placeholder string with the following instruction:

    ```python
    # Define MA Agent instructions
    MA_AGENT_INSTRUCTIONS = """
    You are "MA Agent" — an AI assistant that helps managers review and explore Payment Voucher (PV) requests.

    YOUR ROLE:
    - Help managers understand and analyze PV request details shared in the conversation
    - Summarize PV data clearly and concisely
    - Identify missing fields, potential issues, or anomalies in PV data
    - Answer questions about PV content based on the data provided
    - Highlight important information such as expense amounts, requestors, and approval status

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
            "monthlyBudget": 0
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
    - status "ReadyForSubmission": Complete and ready for review

    CONSTRAINTS:
    - You do NOT approve or reject PV requests
    - You do NOT modify PV data
    - You do NOT create new PV requests
    - If PV data is incomplete, note which fields are missing but do not fill them in
    - Always base your analysis on the exact data provided — do not invent or assume unreported values
    """
    ```

### Initialize the agent and start the conversation loop

1. Find the comment `# Initialize a credential` and add:

    ```python
    # Initialize a credential
    credential = AzureCliCredential()
    ```

1. Find the comment `# Create the agent and start a conversation loop` and add:

    ```python
    # Create the agent and start a conversation loop
    async with Agent(
        client=AzureOpenAIResponsesClient(
            credential=credential,
            deployment_name=os.getenv("MODEL_DEPLOYMENT_NAME"),
            project_endpoint=os.getenv("PROJECT_ENDPOINT"),
        ),
        instructions=MA_AGENT_INSTRUCTIONS,
    ) as agent:

        # return agent
        
        conversation_history = []

        print("MA Agent is ready. Type 'quit' to exit.\n")

        # Conversation loop
        while True:
            user_input = input("You: ").strip()
            if user_input.lower() == "quit":
                break
            if not user_input:
                continue

            # Build context from recent history and send the message
            recent_context = "\n".join(conversation_history[-8:])
            response = await agent.run([f"{recent_context}\nUser: {user_input}"])
            print(f"\nAgent: {response}\n")

            conversation_history.append(f"User: {user_input}")
            conversation_history.append(f"Agent: {response}")
    ```

1. Save the file (**Ctrl+S**).

### Run the agent

1. In the terminal (with your virtual environment active), run:

    ```bash
    python ma_agent.py
    ```

    > **Tip**: If you see an authentication error, run `az login` in the terminal and try again.

1. When the agent starts, you should see:

    ```
    MA Agent - Manager Assistant
    ========================================
    MA Agent is ready. Type 'quit' to exit.
    ```

1. The agent is now running. Continue to **Part 3** to test it with manager scenarios.

---

## Part 3: Test with Manager Scenarios

Test your MA Agent using two realistic scenarios. Paste the prompts below directly into the running agent terminal. Observe how the agent interprets PV JSON and responds to manager questions.

### Scenario 1 — Review a Single PV

At the `You:` prompt, enter the following:

```
Here is a pending PV request submitted by one of my team members. Can you summarize it and tell me if anything looks concerning?

{"pv": {"pvTitle": "Monthly GitHub Copilot Subscription", "requestDate": "2026-03-01", "requestor": {"name": "Somchai Jaidee"}, "payee": {"name": "GitHub Inc."}, "purpose": {"for": "Monthly developer tool subscription", "objective": "Improve developer productivity for IT Internal project"}, "expense": {"type": "MonthlyFee", "amount": {"value": 5000, "currency": "THB"}}, "project": {"projectName": "IT Internal", "budgetSummary": {"monthlyBudget": 50000}}, "approval": {"approverName": "Napat Ruangroj", "status": "Pending"}, "status": "ReadyForSubmission"}}
```

**Expected behavior:**
- Agent provides a clear, readable summary of the PV
- Agent notes that `status` is `ReadyForSubmission` — this is ready for your review
- Agent identifies no critical issues since all fields are present
- Agent does NOT approve or modify the request

### Scenario 2 — Compare Two PVs and Identify Issues

Type `quit` to exit the current session, then restart the agent (`python ma_agent.py`) to start a fresh conversation. At the `You:` prompt, enter:

```
I have two PV requests to review. Which one is ready for approval and which needs more information?

PV 1: {"pv": {"pvTitle": "Conference Registration Fee", "requestDate": "2026-03-15", "requestor": {"name": "Prasert Kaewkla"}, "payee": {"name": "TechConf Thailand"}, "purpose": {"for": "Annual developer conference registration", "objective": "Team learning and networking"}, "expense": {"type": "OneTime", "amount": {"value": 3500, "currency": "THB"}}, "project": {"projectName": "HR Development", "budgetSummary": {"monthlyBudget": 20000}}, "approval": {"approverName": "Manee Srisuk", "status": "Pending"}, "status": "ReadyForSubmission"}}

PV 2: {"pv": {"pvTitle": "USB-C Hub Purchase", "requestDate": "2026-03-20", "requestor": {"name": "Chokchai Phongphan"}, "payee": {"name": ""}, "purpose": {"for": "Hardware purchase", "objective": ""}, "expense": {"type": "OneTime", "amount": {"value": 1200, "currency": "THB"}}, "project": {"projectName": "", "budgetSummary": {"monthlyBudget": 0}}, "approval": {"approverName": "", "status": "Pending"}, "status": "Draft"}}
```

**Expected behavior:**
- Agent identifies PV 1 as complete and `ReadyForSubmission`
- Agent identifies PV 2 as `Draft` with multiple missing fields: `payee.name`, `purpose.objective`, `project.projectName`, and `approval.approverName`
- Agent clearly lists what is missing in PV 2 without inventing or filling in the gaps
- Agent does NOT approve either request

> **Observation question**: Does the agent clearly distinguish between a complete and an incomplete PV? Does it avoid making up missing information? If not, revisit the `MA_AGENT_INSTRUCTIONS` in `ma_agent.py` and re-run.

1. When you're satisfied with both scenarios, type `quit` to exit.

---

## Deliverables

After completing this exercise, you should have:

- ✅ A working `ma_agent.py` that creates and runs the MA Agent at runtime via the Agent Framework SDK
- ✅ Tested two manager review scenarios in the terminal
- ✅ Confirmed that the agent correctly identifies complete vs. incomplete PV entries

---

## Summary

In this exercise, you:

- Described the MA Agent's role as a manager-facing companion to the PV Agent
- Designed an agent instruction that understands and interprets PV JSON data
- Implemented the MA Agent conversation loop in Python using the Microsoft Agent Framework SDK, creating the agent entirely at runtime without the portal
- Tested two scenarios in the terminal: a clean PV review and a comparison that exposed missing fields

In the next module (**D4-M2**), you'll add a **Cosmos DB query tool** so the MA Agent can retrieve PV records directly from the database instead of relying on manually pasted data.
