---
lab:
    title: 'D3-M2: PV Agent — Agent Role, Tasks, and Conversation Design'
    description: 'Design the PV Agent instruction, define the conversation flow for slot-filling, and test with user stories using the Microsoft Foundry portal and Agent Framework SDK.'
    level: 200
    duration: 45
    islab: true
---

# D3-M2: PV Agent — Agent Role, Tasks, and Conversation Design

In this exercise, you'll design the PV Agent's identity and conversation behavior. You'll write an agent instruction, define a conversation flow that collects PV fields from users, test it with multiple user stories in the Foundry portal, then implement the agent in Python using the Microsoft Agent Framework SDK.


> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## Learning Objectives

By the end of this exercise, you'll be able to:

1. Write a clear agent instruction (system prompt) that defines role, rules, and output format
2. Identify required and optional fields for a PV document
3. Design a slot-filling conversation flow in natural language
4. Test the agent with different user story patterns in the Foundry playground
5. Implement the agent conversation loop using the Microsoft Agent Framework SDK

## Prerequisites

- Completed **D3-M1** (Foundry project created, GPT-4.1 deployed, `.env` configured)
- Microsoft Foundry VS Code extension installed and connected
- Python virtual environment activated with packages installed from D3-M1

## Scenario

Before writing any code, you need to define what the PV Agent knows, what it can do, and how it talks to users. You'll approach this the way a product team would — by writing a clear instruction document and validating it with real conversation scenarios in the portal.

---

## Part 1: Define the PV Agent Role

The agent instruction (system prompt) is the most important input to an AI agent. A well-designed instruction produces consistent, accurate, and safe behavior.


### Design the Conversation Flow

A good PV Agent follows a **slot-filling** pattern:

1. Greet the user and understand their intent
2. Identify what type of PV they need (MonthlyFee or OneTime)
3. Collect mandatory fields — ask ONE question at a time for missing fields
4. Confirm the summary with the user
5. Set `status = ReadyForSubmission` and output the final JSON

**Important design rules:**
- Never ask for a field the user already provided
- Never invent data that the user did not provide
- Always state your assumptions explicitly (e.g., "I'll use THB as the default currency")
- Only output the final JSON after user confirmation

---

## Part 2: Create the Agent Instruction in the Foundry Portal

Now you'll create your PV Agent directly in the Foundry portal and write its instruction.

### Create the agent

1. Open the [Foundry portal](https://ai.azure.com) and navigate to your `[username]-pv-agent` project.

1. In the left navigation, select **Build** > **Agents**.

1. Select **+ New agent**.

1. Set the **Agent name** to `pv-agent` and select **Create**.

1. The agent playground will open. The GPT-4.1 model should be selected automatically.

### Write the agent instruction

1. In the **Instructions** field, paste the following instruction text:

    ```
    You are "PV Agent" — an AI assistant that helps requestors draft a Payment Voucher (PV) request.

    YOUR ROLE:
    - Guide the user through collecting all required PV fields via natural conversation
    - Ask only for missing critical fields, one at a time
    - State your assumptions explicitly (for example, "I'll use THB as the default currency")
    - Show a confirmation summary before marking the PV as ReadyForSubmission
    - Always output a valid JSON object as the final step

    REQUIRED FIELDS (must be collected before ReadyForSubmission):
    - pvTitle: Short, descriptive title for the voucher
    - requestDate: Date in YYYY-MM-DD format
    - requestor.name: Full name of the person making the request
    - payee.name: Full name or company of who will receive the payment
    - purpose.for: What specifically is being paid for
    - purpose.objective: The business reason or objective
    - expense.type: MUST be exactly "MonthlyFee" or "OneTime" — map user's words to one of these two values
    - expense.amount.value: A positive number — if user says text like "one thousand", convert to 1000
    - expense.amount.currency: Default to "THB" unless user specifies otherwise
    - project.projectName: Name of the project this payment belongs to — never invent, always ask user
    - approval.approverName: Name of the approver

    CONSTRAINTS:
    - You do NOT approve requests or change budget limits
    - You do NOT invent project names, payee names, or amounts
    - approval.status is always "Pending" for new requests
    - Set status to "ReadyForSubmission" only after user confirms the summary
    - Keep status as "Draft" otherwise

    OUTPUT FORMAT (produce this JSON when all fields are collected and confirmed):
    {
    "pv": {
        "pvTitle": "...",
        "requestDate": "YYYY-MM-DD",
        "requestor": { "name": "..." },
        "payee": { "name": "..." },
        "purpose": {
        "for": "...",
        "objective": "..."
        },
        "expense": {
        "type": "MonthlyFee | OneTime",
        "amount": { "value": 0, "currency": "THB" }
        },
        "project": {
        "projectName": "...",
        "budgetSummary": {
            "monthlyBudget": 0
        }
        },
        "approval": { "approverName": "...", "status": "Pending" },
        "status": "Draft | ReadyForSubmission"
    }
    }
    ```

1. Select **Save** (or **Apply**) to save the agent configuration.

---

## Part 3: Test with User Stories in the Playground

Test your agent instruction using three different user stories. Observe how the agent handles each case.

### User Story 1 — Monthly Subscription (Happy Path)

Start a **new chat session** in the playground, then enter the following prompt:

```
I need to create a PV for our monthly GitHub Copilot subscription. 
The cost is 5,000 baht per month. It's for the IT Internal project.
My name is Somchai Jaidee and the approver is Napat Ruangroj.
```

**Expected behavior:**
- Agent identifies this as a `MonthlyFee` type
- Agent maps to `Expense` budget type
- Agent asks for any missing fields (payee, request date, etc.)
- Agent does NOT set `ReadyForSubmission` until user confirms

Take note of which fields the agent asks for and whether the conversation feels natural.

### User Story 2 — One-Time Purchase

Start a **new chat session**, then enter:

```
I want to pay for a one-time purchase of a USB-C hub for my workstation.
The price is 1,200 baht.
```

**Expected behavior:**
- Agent identifies as `OneTime` type
- Agent asks for missing fields one at a time (requestor, payee, project, approver, etc.)
- Agent explicitly asks for the project name — it must NOT invent one

### User Story 3 — Missing Project Name

Start a **new chat session**, then enter:

```
Please create a PV for training workshop registration fee, 3,500 baht,
for Prasert Kaewkla. The approver is Manee Srisuk.
My name is Chokchai Phongphan.
```

**Expected behavior:**
- Agent correctly collects most fields
- Agent **must ask** for the project name — it must NOT guess or invent it
- Agent waits for user to provide the project name before proceeding

> **Observation question**: Does your agent always ask for missing fields rather than inventing values? If not, adjust the instruction and re-test.

---

## Part 4: Implement the Agent in Python

### Configure the Python virtual environment

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



Now you'll bring the agent design into code using the Microsoft Agent Framework SDK.

### Open the starter code

1. In VS Code, navigate to the `labs/02-pv-agent/02-agent-foundation/Labfiles/` folder.

1. Open the `pv_agent.py` file. Review the existing structure — it has placeholder comments to guide you.

1. Open the `.env.example` file, save a copy as `.env`, and fill in your project endpoint and model name. (You can reuse the `.env` values from D3-M1.)

### Add references

1. At the top of `pv_agent.py`, find the comment `# Add references` and add the following imports:

    ```python
    # Add references
    from agent_framework import Agent
    from agent_framework.azure import AzureOpenAIResponsesClient
    from azure.identity import AzureCliCredential
    ```

### Add the PV Agent instruction

1. Find the comment `# Define PV Agent instructions` and replace the placeholder string with the instruction you designed in Part 2:

    ```python
    # Define PV Agent instructions
    PV_AGENT_INSTRUCTIONS = """
    You are "PV Agent" — an AI assistant that helps requestors draft a Payment Voucher (PV) request.

    YOUR ROLE:
    - Guide the user through collecting all required PV fields via natural conversation
    - Ask only for missing critical fields, one at a time
    - State your assumptions explicitly (for example, "I'll use THB as the default currency")
    - Show a confirmation summary before marking the PV as ReadyForSubmission
    - Always output a valid JSON object as the final step

    REQUIRED FIELDS (must be collected before ReadyForSubmission):
    - pvTitle: Short, descriptive title for the voucher
    - requestDate: Date in YYYY-MM-DD format
    - requestor.name: Full name of the person making the request
    - payee.name: Full name or company of who will receive the payment
    - purpose.for: What specifically is being paid for
    - purpose.objective: The business reason or objective
    - expense.type: MUST be exactly "MonthlyFee" or "OneTime" — map user's words to one of these two values
    - expense.amount.value: A positive number — if user says text like "one thousand", convert to 1000
    - expense.amount.currency: Default to "THB" unless user specifies otherwise
    - project.projectName: Name of the project this payment belongs to — never invent, always ask user
    - approval.approverName: Name of the approver

    CONSTRAINTS:
    - You do NOT approve requests or change budget limits
    - You do NOT invent project names, payee names, or amounts
    - approval.status is always "Pending" for new requests
    - Set status to "ReadyForSubmission" only after user confirms the summary
    - Keep status as "Draft" otherwise

    OUTPUT FORMAT (produce this JSON when all fields are collected and confirmed):
    {
        "pv": {
            "pvTitle": "...",
            "requestDate": "YYYY-MM-DD",
            "requestor": { "name": "..." },
            "payee": { "name": "..." },
            "purpose": {
            "for": "...",
            "objective": "..."
            },
            "expense": {
            "type": "MonthlyFee | OneTime",
            "amount": { "value": 0, "currency": "THB" }
            },
            "project": {
            "projectName": "...",
            "budgetSummary": {
                "monthlyBudget": 0
            }
            },
            "approval": { "approverName": "...", "status": "Pending" },
            "status": "Draft | ReadyForSubmission"
        }
    }
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
        instructions=PV_AGENT_INSTRUCTIONS,
    ) as agent:
        conversation_history = []

        print("\nPV Agent is ready. Type 'quit' to exit.\n")

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
    python pv_agent.py
    ```

1. Test the three user stories from Part 3, observing how the agent handles each case.

1. When you're satisfied, type `quit` to exit.

---

## Deliverables

After completing this exercise, you should have:

- ✅ An agent named `pv-agent` in your Foundry project with the instruction defined
- ✅ Tested three user story scenarios in the portal playground
- ✅ A working `pv_agent.py` that runs the agent conversation loop
- ✅ Notes on how the agent handles missing fields vs. happy-path inputs

---

## Summary

In this exercise, you:

- Defined the PV Agent's role, boundaries, and required fields
- Designed a slot-filling conversation flow
- Wrote and tested an agent instruction in the Foundry portal
- Implemented the agent in Python with the Microsoft Agent Framework SDK
- Validated the design against three realistic user stories

In the next module (**D3-M3**), you'll add a **data validation layer** to enforce PV JSON quality before submission.
