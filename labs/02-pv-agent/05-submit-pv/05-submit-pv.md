---
lab:
    title: 'D4-M1: PV Agent — Submit PV to Azure Cosmos DB'
    description: 'Add a submit_pv function tool that receives the completed PV JSON and prepares it for persistence in Azure Cosmos DB. The agent instruction is tuned to ensure all fields are collected before triggering the submission tool.'
    level: 200
    duration: 30
    islab: true
---

# D4-M1: PV Agent — Submit PV to Azure Cosmos DB

In this exercise, you'll add a second function tool — `submit_pv` — to the PV Agent. When the user confirms a completed Payment Voucher, the agent calls this tool and passes the full PV JSON object to it. The tool prints the JSON to the terminal as a placeholder for actual Cosmos DB insertion, which you'll implement in a later exercise.

You'll also tune the agent instruction so the agent **never calls `submit_pv` until every required field is filled** — ensuring the data arriving at the tool is always complete and insertion-ready.

This exercise takes approximately **30** minutes.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## Learning Objectives

By the end of this exercise, you'll be able to:

1. Define a function tool that accepts a structured JSON string as input
2. Tune an agent instruction to gate tool calls behind data completeness checks
3. Verify the agent hands off the right payload to the tool
4. Understand the pattern of separating data collection from persistence

## Prerequisites

- Completed **D3-M4** (PV Agent running with `get_project_budget` tool, `.env` configured)
- Microsoft Foundry VS Code extension installed and connected
- Python virtual environment activated with packages installed

## Scenario

The finance team is ready to store Payment Vouchers in Azure Cosmos DB. Before writing the actual database code, you need to:

1. Define the tool interface the agent will call
2. Make sure the agent always passes a **complete, valid** PV JSON — no missing fields, no `"Draft"` status
3. Validate the integration end-to-end by inspecting the JSON printed to the terminal

This pattern — stubbing the persistence layer first and validating the payload shape — is a standard practice before wiring up cloud databases.

---

## Part 1: Understand the Tool Pattern for Structured Payloads

### Passing JSON from the agent to a tool

In D3-M4 you built a tool that *returns* data to the agent (budget lookup). In this exercise you'll build a tool that *receives* data from the agent — the completed PV JSON.

The pattern is the same, but the data flow is reversed:

```
User confirms PV
    → Agent assembles full JSON
        → Agent calls submit_pv(pv_json="{ ... }")
            → Tool receives the string, parses and processes it
```

The parameter type is `str` (a JSON-encoded string). The `Field(description=...)` annotation tells the agent exactly what to put in that string.

### Gating the tool call with instruction rules

Without explicit rules, the agent might call `submit_pv` too early — before all fields are collected — or it might forget to call it at all. You'll add two rules to `PV_AGENT_INSTRUCTIONS`:

1. **Completeness gate** — only call `submit_pv` when `status` is `"ReadyForSubmission"` and all required fields are non-empty
2. **Trigger rule** — call `submit_pv` immediately after the user confirms the summary

---

## Part 2: Open the Starter Code

1. In VS Code, navigate to `labs/02-pv-agent/05-submit-pv/Labfiles/`.

1. Open `pv_agent.py`. The file contains the PV Agent from D3-M4, including the `get_project_budget` tool. This is your starting point.

1. Open `.env.example`, save a copy as `.env`, and fill in your project endpoint and model deployment name.

---

## Part 3: Implement the `submit_pv` Tool

1. Find the comment `# Create a tool function to submit the PV to Cosmos DB` in the file, located after the `get_project_budget` function.

1. Add the following function below that comment:

    ```python
    # Create a tool function to submit the PV to Cosmos DB
    @tool(approval_mode="never_require")
    def submit_pv(
        pv_json: Annotated[str, Field(description="The complete PV JSON object as a string, with status set to ReadyForSubmission and all required fields filled")]
    ) -> str:
        """Receive the completed PV JSON and prepare it for insertion into Azure Cosmos DB."""
        print("\n--- PV Submission Received ---")
        print(pv_json)
        print("--- End of PV Submission ---\n")
        return "PV submission received and ready for database insertion."
    ```

    Key points:
    - The `description` in `Annotated[str, Field(...)]` is instructions to the agent — it tells the agent to only call this function when the JSON is complete and the status is `ReadyForSubmission`
    - The function body only prints the JSON for now — actual Cosmos DB insertion comes in a later exercise
    - The return value is a confirmation string the agent will use to reply to the user

---

## Part 4: Register the Tool with the Agent

1. Find the `tools=[get_project_budget]` line inside the `Agent(...)` constructor.

1. Add `submit_pv` to the list:

    ```python
    tools=[get_project_budget, submit_pv],
    ```

---

## Part 5: Tune the Agent Instruction

The agent needs explicit rules about when to call `submit_pv` and what to include in the payload. You'll update `PV_AGENT_INSTRUCTIONS` in two places.

### Update YOUR ROLE

1. Find the `YOUR ROLE` section in `PV_AGENT_INSTRUCTIONS`.

1. Add the following two lines at the end of the role block:

    ```
    - Do NOT call submit_pv until the user has confirmed the summary and ALL required fields are filled with real values (no placeholders, no empty strings, no zeros for required amounts).
    - After the user confirms the PV summary, call submit_pv with the complete PV JSON string (status must be "ReadyForSubmission").
    ```

### Update the CONSTRAINTS section

1. Find the `CONSTRAINTS` section.

1. Add the following constraint:

    ```
    - Never call submit_pv with a PV that has status "Draft" or has any required field missing or set to a placeholder value.
    ```

1. Save the file (**Ctrl+S**).

---

## Part 6: Run the Agent

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

### Run the agent

1. In the terminal (with your virtual environment active), run:

    ```bash
    python pv_agent.py
    ```

1. Test using the user stories below.

---

## Part 7: Test with User Stories

Use the following user stories to verify that the agent correctly collects all fields, confirms the summary, and then calls `submit_pv` with a complete JSON payload.

### User Story 1 — Full Data Provided Up Front

```
I need to create a PV for our monthly GitHub Copilot subscription.
The cost is 5,000 baht per month. Project is IT Internal.
My name is Somchai Jaidee. Payee is GitHub Inc.
Purpose is to support developer productivity tools. Approver is Napat Ruangroj.
```

**Expected behavior:**
- Agent collects all fields, calls `get_project_budget` for "IT Internal"
- Agent shows a confirmation summary
- After you confirm (type "yes" or "confirm"), agent calls `submit_pv`
- Terminal prints the complete PV JSON between the `--- PV Submission Received ---` markers
- `status` in the printed JSON is `"ReadyForSubmission"`
- `project.budgetSummary` contains real values from the CSV

### User Story 2 — Partial Data, Agent Asks for Missing Fields

```
I want to submit a PV to purchase 5 ergonomic chairs.
Total cost is 25,000 baht.
```

**Expected behavior:**
- Agent identifies missing fields and asks for them ONE at a time (requestor, payee, project, purpose, approver, etc.)
- Once all fields are provided, agent calls `get_project_budget` for the given project
- Agent shows a confirmation summary and waits for user confirmation
- After confirmation, agent calls `submit_pv` with the complete JSON
- Agent does NOT call `submit_pv` while any field is still missing

### User Story 3 — User Provides All Fields Across Multiple Messages

Start a new session and answer the agent's questions one by one:

1. First message: `I need to create a PV for a workshop registration fee, 3,500 baht.`
2. Answer each question the agent asks until all fields are filled
3. Confirm the summary when shown

**Expected behavior:**
- Agent asks for each missing field individually
- Agent calls `get_project_budget` when you provide the project name
- After your confirmation, agent calls `submit_pv` once, with the complete JSON
- The printed JSON has no empty or placeholder values

> **Observation question**: Does the agent ever call `submit_pv` before you confirm the summary? If so, review the instruction rules you added and refine the wording. Instruction tuning is iterative — small wording changes can significantly affect agent behavior.

---

## Deliverables

After completing this exercise, you should have:

- ✅ Implemented `submit_pv` with the `@tool` decorator and a `pv_json` annotated parameter
- ✅ Added `submit_pv` to the `tools=[...]` list alongside `get_project_budget`
- ✅ Updated `PV_AGENT_INSTRUCTIONS` with completeness gate and trigger rules
- ✅ Tested all three user stories and confirmed the terminal output shows correct, complete JSON
- ✅ Verified `status` is always `"ReadyForSubmission"` in the printed payload
