---
lab:
    title: 'D4-M2: MA Agent — Function Tools for PV Review and Approval'
    description: 'Extend the MA Agent with two custom function tools: one to retrieve PV requests filtered by approval status and one to update PV approval status. Uses sample data as a placeholder for Cosmos DB integration.'
    level: 200
    duration: 30
    islab: true
---

# D4-M2: MA Agent — Function Tools for PV Review and Approval

In this exercise, you'll extend the MA Agent with two **custom function tools** using the `@tool` decorator from the Microsoft Agent Framework SDK. These tools give the agent the ability to:

1. **Retrieve** PV requests filtered by approval status (`Pending` or `Approved`)
2. **Update** the approval status of a specific PV request

You'll use in-memory sample data as a placeholder — actual Cosmos DB integration comes in a later exercise.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## Learning Objectives

By the end of this exercise, you'll be able to:

1. Explain the `@tool` decorator pattern and how the agent decides when to call a tool
2. Implement a function tool that filters and returns structured JSON data
3. Implement a function tool that updates data and returns a confirmation
4. Register multiple tools with the Agent and verify the agent calls them correctly
5. Update the agent instruction to guide tool usage behavior

## Prerequisites

- Completed **D4-M1** (MA Agent running with instruction, `.env` configured)
- Microsoft Foundry VS Code extension installed and connected
- Python virtual environment activated with packages installed

## Scenario

Your organization's managers now want the MA Agent to be more than a passive reader — they want to ask the agent for PV requests by status and approve them directly through conversation. In this exercise, you'll build the two tools that enable this workflow:

- `get_pv_requests(approval_status)` — retrieves PV entries matching a given approval status
- `update_pv_approval_status(pv_id, new_status)` — changes a PV's approval status

For now, both tools work against **sample data stored in a Python list**. This lets you focus on the tool pattern and agent instruction tuning without worrying about database connectivity.

---

## Part 1: Understand Function Call Tools

### When to use a tool vs. instruction grounding

| Approach | Best for |
|---|---|
| Instruction grounding | Classification rules, enum derivation, format rules |
| Function call tool | Data lookup, data updates, calculations, external API calls, I/O operations |

The `@tool` decorator marks a regular Python function as a capability the agent can invoke. The agent decides *when* to call it based on the tool's description and the conversation context.

### The `@tool` decorator pattern

Here is the pattern used in the Microsoft Agent Framework SDK:

```python
from agent_framework import tool
from pydantic import Field
from typing import Annotated

@tool(approval_mode="never_require")
def my_tool(
    param: Annotated[str, Field(description="Description of the parameter")]
) -> str:
    # perform your logic here
    return "result string"
```

Key points:
- `@tool(approval_mode="never_require")` tells the agent to call this function automatically without asking for user approval each time
- `Annotated[type, Field(description="...")]` provides the agent with a natural-language description of each parameter — the agent uses this to fill in the arguments
- The function must return a value (typically a `str`) that the agent will use as context

### Registering tools with the Agent

Once the functions are defined, pass them in the `tools` list when creating the `Agent`:

```python
async with Agent(
    client=...,
    instructions=...,
    tools=[tool_a, tool_b],
) as agent:
```

---

## Part 2: Open the Starter Code

### Configure the Python virtual environment

1. In VS Code, navigate to `labs/03-ma-agent/02-function-tools/Labfiles/`.

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

### Open the starter files

1. Open `ma_agent.py`. The file contains the MA Agent from D4-M1, with the instruction and conversation loop already in place. You'll see placeholder comments where you'll add the new code.

1. Open `.env.example`, save a copy as `.env`, and fill in your project endpoint and model deployment name.

---

## Part 3: Add References

1. Find the comment `# Add references` near the top of the file, after the existing import statements.

1. Add the following imports:

    ```python
    # Add references
    from agent_framework import tool, Agent
    from agent_framework.azure import AzureOpenAIResponsesClient
    from azure.identity import AzureCliCredential
    from pydantic import Field
    from typing import Annotated
    ```

    > **Note**: `tool` is the decorator that marks your function as an agent tool. `Field` and `Annotated` are used to attach descriptions to each parameter so the agent knows how to use them.

---

## Part 4: Add Sample PV Data

Before implementing the tools, you need sample PV data for them to work with. This data structure matches what the PV Agent would store in Cosmos DB.

1. Below the `MA_AGENT_INSTRUCTIONS` variable and above the tool function comments, add the following sample data:

    ```python
    # Sample PV data (placeholder for Cosmos DB integration in a later exercise)
    SAMPLE_PV_DATA = [
        {
            "id": "pv-001",
            "pvTitle": "Monthly GitHub Copilot Subscription",
            "requestDate": "2026-03-01",
            "requestor": {"name": "Somchai Jaidee"},
            "payee": {"name": "GitHub Inc."},
            "purpose": {
                "for": "Monthly developer tool subscription",
                "objective": "Improve developer productivity for IT Internal project"
            },
            "expense": {
                "type": "MonthlyFee",
                "amount": {"value": 5000, "currency": "THB"}
            },
            "project": {
                "projectName": "IT Internal",
                "budgetSummary": {"monthlyBudget": 50000}
            },
            "approval": {"approverName": "Napat Ruangroj", "status": "Pending"},
            "status": "ReadyForSubmission"
        },
        {
            "id": "pv-002",
            "pvTitle": "Conference Registration Fee",
            "requestDate": "2026-03-15",
            "requestor": {"name": "Prasert Kaewkla"},
            "payee": {"name": "TechConf Thailand"},
            "purpose": {
                "for": "Annual developer conference registration",
                "objective": "Team learning and networking"
            },
            "expense": {
                "type": "OneTime",
                "amount": {"value": 3500, "currency": "THB"}
            },
            "project": {
                "projectName": "HR Development",
                "budgetSummary": {"monthlyBudget": 20000}
            },
            "approval": {"approverName": "Manee Srisuk", "status": "Approved"},
            "status": "ReadyForSubmission"
        },
        {
            "id": "pv-003",
            "pvTitle": "Ergonomic Office Chairs",
            "requestDate": "2026-03-20",
            "requestor": {"name": "Wanchai Teeraphon"},
            "payee": {"name": "Office Furniture Co., Ltd."},
            "purpose": {
                "for": "Purchase of 5 ergonomic chairs",
                "objective": "Improve workspace comfort for HR team"
            },
            "expense": {
                "type": "OneTime",
                "amount": {"value": 25000, "currency": "THB"}
            },
            "project": {
                "projectName": "HR Internal",
                "budgetSummary": {"monthlyBudget": 100000}
            },
            "approval": {"approverName": "Manee Srisuk", "status": "Approved"},
            "status": "ReadyForSubmission"
        }
    ]
    ```

    This gives you **3 PV entries**: 1 with `Pending` status and 2 with `Approved` status — enough to test both filtering and status updates.

---

## Part 5: Implement the `get_pv_requests` Tool

1. Find the comment `# Create a tool function to get PV requests by approval status` in the file.

1. Add the following function below that comment:

    ```python
    # Create a tool function to get PV requests by approval status
    @tool(approval_mode="never_require")
    def get_pv_requests(
        approval_status: Annotated[str, Field(description="The approval status to filter by. Must be exactly 'Pending' or 'Approved'.")]
    ) -> str:
        """Retrieve PV requests filtered by their approval status (Pending or Approved)."""
        if approval_status not in ("Pending", "Approved"):
            return f"Invalid approval status '{approval_status}'. Must be 'Pending' or 'Approved'."

        filtered = [pv for pv in SAMPLE_PV_DATA if pv["approval"]["status"] == approval_status]

        if not filtered:
            return f"No PV requests found with approval status '{approval_status}'."

        return json.dumps(filtered, indent=2, ensure_ascii=False)
    ```

    Key points:
    - The `approval_status` parameter is annotated so the agent knows the valid values
    - The function validates the input before filtering
    - Results are returned as a JSON string the agent can read and summarize
    - If no matches are found, a descriptive message is returned instead

---

## Part 6: Implement the `update_pv_approval_status` Tool

1. Find the comment `# Create a tool function to update approval status of a PV request` in the file.

1. Add the following function below that comment:

    ```python
    # Create a tool function to update approval status of a PV request
    @tool(approval_mode="never_require")
    def update_pv_approval_status(
        pv_id: Annotated[str, Field(description="The unique id of the PV request to update")],
        new_status: Annotated[str, Field(description="The new approval status. Must be exactly 'Pending' or 'Approved'.")]
    ) -> str:
        """Update the approval status of a specific PV request by its id."""
        if new_status not in ("Pending", "Approved"):
            return f"Invalid status '{new_status}'. Must be 'Pending' or 'Approved'."

        for pv in SAMPLE_PV_DATA:
            if pv["id"] == pv_id:
                old_status = pv["approval"]["status"]
                pv["approval"]["status"] = new_status
                print(f"\n[Update] PV '{pv_id}' approval status changed: {old_status} → {new_status}\n")
                return f"PV '{pv_id}' ({pv['pvTitle']}) approval status updated from '{old_status}' to '{new_status}' successfully."

        return f"PV with id '{pv_id}' not found."
    ```

    Key points:
    - The function requires two parameters: `pv_id` to identify the target PV and `new_status` for the new approval value
    - Input validation ensures only `Pending` or `Approved` are accepted
    - The status update happens in the in-memory `SAMPLE_PV_DATA` list (Cosmos DB integration will replace this later)
    - The function prints a log line to the terminal so you can visually confirm the update

---

## Part 7: Register the Tools with the Agent

1. Find the `Agent(...)` constructor inside the `async with` block in `main()`.

1. Find the comment `# Add tools list here` and replace it with the `tools` parameter:

    ```python
    tools=[get_pv_requests, update_pv_approval_status],
    ```

---

## Part 8: Update the Agent Instruction

The agent needs to know it has tools available and when to use them. You'll add rules to `MA_AGENT_INSTRUCTIONS`.

### Update YOUR ROLE

1. Find the `YOUR ROLE` section in `MA_AGENT_INSTRUCTIONS`.

1. Add the following two lines at the end of the role block:

    ```
    - When the manager asks to see PV requests, call the get_pv_requests tool with the appropriate approval status filter
    - When the manager asks to approve or set a PV back to pending, call the update_pv_approval_status tool with the PV id and the new status
    ```

### Update the CONSTRAINTS section

1. Find the `CONSTRAINTS` section.

1. Add the following constraint:

    ```
    - When updating approval status, you MUST use the exact PV id from the data returned by get_pv_requests
    ```

1. Save the file (**Ctrl+S**).

---

## Part 9: Run the Agent

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

1. Test using the scenarios below.

---

## Part 10: Test with User Stories

Use the following scenarios to verify that the agent correctly calls both tools and handles the data properly.

### Scenario 1 — View Pending PV Requests and Approve One

At the `You:` prompt, enter:

```
Show me all pending PV requests that need my review.
```

**Expected behavior:**
- Agent calls `get_pv_requests` with `approval_status="Pending"`
- Agent presents a summary of the pending PV(s) — you should see the GitHub Copilot subscription (pv-001)
- Agent does NOT invent additional PV entries

Now approve the pending request by entering:

```
Please approve the GitHub Copilot subscription PV.
```

**Expected behavior:**
- Agent calls `update_pv_approval_status` with `pv_id="pv-001"` and `new_status="Approved"`
- Terminal prints: `[Update] PV 'pv-001' approval status changed: Pending → Approved`
- Agent confirms the approval to you

### Scenario 2 — View Approved PV Requests

Type `quit`, restart the agent (`python ma_agent.py`), then enter:

```
Can you show me all PV requests that have already been approved?
```

**Expected behavior:**
- Agent calls `get_pv_requests` with `approval_status="Approved"`
- Agent summarizes the two approved PVs (Conference Registration Fee and Ergonomic Office Chairs)
- Agent presents relevant details: amounts, requestors, projects

> **Observation question**: Does the agent always use the tools to retrieve data, or does it sometimes try to answer from memory? If it skips the tool, revisit your instruction and reinforce the rule. Does the agent use the exact `pv_id` when updating? Small instruction wording changes can significantly affect tool invocation behavior.

---

## Deliverables

After completing this exercise, you should have:

- ✅ Added `import json` and references for `tool`, `Field`, and `Annotated`
- ✅ Added 3 sample PV entries (1 Pending, 2 Approved) as in-memory data
- ✅ Implemented `get_pv_requests` with the `@tool` decorator and status filtering
- ✅ Implemented `update_pv_approval_status` with the `@tool` decorator and id-based lookup
- ✅ Registered both tools in the `Agent` constructor via `tools=[get_pv_requests, update_pv_approval_status]`
- ✅ Updated `MA_AGENT_INSTRUCTIONS` with tool usage rules
- ✅ Tested both scenarios and confirmed correct tool invocation behavior

---

## Summary

In this exercise, you:

- Learned the `@tool` decorator pattern for defining agent-callable functions
- Implemented two function tools: one for data retrieval and one for data updates
- Used sample PV data as an in-memory placeholder for Cosmos DB
- Tuned the agent instruction to guide when and how the agent uses each tool
- Verified end-to-end tool invocation with realistic manager workflows

In the next module, you'll replace the in-memory sample data with **live Cosmos DB queries**, connecting the MA Agent to the same database where the PV Agent stores its submissions.
