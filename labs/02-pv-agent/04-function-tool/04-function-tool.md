---
lab:
    title: 'D3-M4: PV Agent — Function Call Tool for Budget Data Lookup'
    description: 'Extend the PV Agent with a custom function tool that reads project budget data from a CSV file using the Microsoft Agent Framework SDK @tool decorator.'
    level: 200
    duration: 30
    islab: true
---

# D3-M4: PV Agent — Function Call Tool for Budget Data Lookup

In this exercise, you'll extend the PV Agent by implementing a **custom function tool** that looks up real project budget data from a CSV file. You'll use the `@tool` decorator from the Microsoft Agent Framework SDK and wire the tool into your agent — no changes to the Foundry portal are needed.



> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## Learning Objectives

By the end of this exercise, you'll be able to:

1. Explain the difference between instruction-based grounding and function call tools
2. Use the `@tool` decorator with type annotations to define an agent tool
3. Read structured data from a CSV file inside a tool function
4. Register a tool with the Agent and verify the agent calls it automatically
5. Test the end-to-end tool invocation with user story scenarios

## Prerequisites

- Completed **D3-M3** (PV Agent running with `expense.budgetType`, `.env` configured)
- Microsoft Foundry VS Code extension installed and connected
- Python virtual environment activated with packages installed

## Scenario

The back-office finance team now requires that each Payment Voucher includes the project's **total budget** and **remaining budget** pulled from the company's project data. This information is stored in a CSV file at `data/projects_budget.csv`.

Unlike the `budgetType` classification from D3-M3 — which could be derived from context — **budget numbers are facts** that must come from a data source. Using a function tool is the correct approach here.

---

## Part 1: Understand Function Call Tools

### When to use a tool vs. instruction grounding

| Approach | Best for |
|---|---|
| Instruction grounding | Classification rules, enum derivation, format rules |
| Function call tool | Data lookup, calculations, external API calls, I/O operations |

The `@tool` decorator marks a regular Python function as a capability the agent can call. The agent decides *when* to call it based on the tool's description and the conversation context.

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

### Registering a tool with the Agent

Once the function is defined, pass it in the `tools` list when creating the `Agent`:

```python
async with Agent(
    client=...,
    instructions=...,
    tools=[my_tool],
) as agent:
```

---

## Part 2: Open the Starter Code

1. In VS Code, navigate to `labs/02-pv-agent/04-function-tool/Labfiles/`.

1. Open `pv_agent.py`. The file contains the PV Agent from D3-M3, complete with the `expense.budgetType` instruction. This is your starting point.

1. Open `.env.example`, save a copy as `.env`, and fill in your project endpoint and model deployment name.

---

## Part 3: Add References

1. Find the comment `# Add references` near the top of the file, after the existing import statements.

1. Add the following imports:

    ```python
    # Add references
    from agent_framework import tool, Agent
    from pydantic import Field
    from typing import Annotated
    ```

    > **Note**: `tool` is the decorator that marks your function as an agent tool. `Field` and `Annotated` are used to attach descriptions to each parameter so the agent knows how to use them.

1. Also add `import csv` at the top of the file alongside the other standard library imports:

    ```python
    import csv
    ```

---

## Part 4: Implement the `get_project_budget` Tool

1. Find the comment `# Create a tool function to look up project budget` in the file, located above the `main()` function.

1. Add the following function below that comment:

    ```python
    # Create a tool function to look up project budget
    @tool(approval_mode="never_require")
    def get_project_budget(
        project_name: Annotated[str, Field(description="The name of the project to look up budget information for")]
    ) -> str:
        """Look up the budget and remaining budget for a given project from the CSV data file."""
        data_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../data/projects_budget.csv")
        try:
            with open(data_path, newline='', encoding='utf-8') as f:
                reader = csv.DictReader(f)
                for row in reader:
                    if row['project name'].strip().lower() == project_name.strip().lower():
                        return f"Project: {row['project name']}, Budget: {row['budget']}, Remaining Budget: {row['remain budget']}"
            return f"Project '{project_name}' not found in the budget data."
        except FileNotFoundError:
            return "Budget data file not found. Please ensure the data file exists."
    ```

    Notice how the function:
    - Uses `os.path` to build a relative path to `data/projects_budget.csv`
    - Reads the CSV using `csv.DictReader` to access named columns
    - Does a case-insensitive match on the `project name` column
    - Returns a descriptive string — the agent will read this and use it to fill in the JSON output

---

## Part 5: Register the Tool with the Agent

1. Find the `Agent(...)` constructor inside the `async with` block in `main()`.

1. Add `tools=[get_project_budget]` as a parameter:

    ```python
    async with Agent(
        client=AzureOpenAIResponsesClient(
            credential=credential,
            deployment_name=os.getenv("MODEL_DEPLOYMENT_NAME"),
            project_endpoint=os.getenv("PROJECT_ENDPOINT"),
        ),
        instructions=PV_AGENT_INSTRUCTIONS,
        tools=[get_project_budget],
    ) as agent:
    ```

---

## Part 6: Update the Agent Instruction

The agent needs to know it has a tool available and what to do with the result. You'll add a rule to the `YOUR ROLE` section of `PV_AGENT_INSTRUCTIONS`.

1. Find the `YOUR ROLE` section in `PV_AGENT_INSTRUCTIONS`.

1. Add the following line at the end of the role description block:

    ```
    - When the user provides a project name, call the get_project_budget tool to look up the project's budget and remaining budget. Use this data to populate project.budgetSummary in the output JSON.
    ```

1. Find the `OUTPUT FORMAT` block and update the `project.budgetSummary` object to use `totalBudget` and `remainingBudget`:

    ```
    "project": {
      "projectName": "...",
      "budgetSummary": {
        "totalBudget": 0,
        "remainingBudget": 0
      }
    },
    ```

1. Save the file (**Ctrl+S**).

---

## Part 7: Run the Agent

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

## Part 8: Test with User Stories

Use the following user stories to verify that the agent correctly calls the `get_project_budget` tool and populates the budget data in the JSON output.

### User Story 1 — Monthly Subscription for a Known Project

```
I need to create a PV for our monthly GitHub Copilot subscription.
The cost is 5,000 baht per month. Project is IT Internal.
My name is Somchai Jaidee. Payee is GitHub Inc.
Purpose is to support developer productivity. Approver is Napat Ruangroj.
```

**Expected behavior:**
- Agent identifies `expense.type` as `MonthlyFee` and `expense.budgetType` as `"Expense"`
- Agent **automatically calls `get_project_budget`** with `"IT Internal"` as the argument
- Final JSON includes `project.budgetSummary.totalBudget` and `project.budgetSummary.remainingBudget` populated from the CSV data

### User Story 2 — Equipment Purchase for Another Known Project

```
I want to submit a PV to purchase 5 ergonomic chairs for the HR team.
Total cost is 25,000 baht. This is for the HR Internal project.
My name is Wanchai Teeraphon. Payee is Office Furniture Co., Ltd.
Purpose is to improve workspace comfort. Approver is Manee Srisuk.
```

**Expected behavior:**
- Agent identifies `expense.type` as `OneTime` and `expense.budgetType` as `"Investment"`
- Agent calls `get_project_budget` with `"HR Internal"`
- Final JSON reflects the budget values from the CSV

### User Story 3 — Unknown Project Name

```
Please create a PV for a workshop registration fee, 3,500 baht.
Payee is Skillbridge Academy. My name is Chokchai Phongphan.
Project is Innovation Lab. Approver is Napat Ruangroj.
Purpose is to upskill the technology team.
```

**Expected behavior:**
- Agent calls `get_project_budget` with `"Innovation Lab"`
- The tool returns a "not found" message
- Agent reports to the user that the project was not found in the budget data and may ask the user to verify the project name
- Agent does NOT invent budget numbers

> **Observation question**: Compare the agent's behavior when the tool returns data (User Stories 1 & 2) vs. when it returns a "not found" message (User Story 3). How does the agent handle each case? Does it communicate the result to the user before proceeding?

---

## Deliverables

After completing this exercise, you should have:

- ✅ Added `import csv` and references for `tool`, `Field`, and `Annotated`
- ✅ Implemented `get_project_budget` with the `@tool` decorator and type annotations
- ✅ Registered the tool in the `Agent` constructor via `tools=[get_project_budget]`
- ✅ Updated `PV_AGENT_INSTRUCTIONS` to instruct the agent to call the tool and populate `budgetSummary`
- ✅ Tested all three user stories and observed correct tool invocation behavior
