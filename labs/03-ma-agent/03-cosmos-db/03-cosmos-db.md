---
lab:
    title: 'D4-M3: MA Agent — Connect Function Tools to Azure Cosmos DB'
    description: 'Replace the in-memory sample data in the MA Agent function tools with real Azure Cosmos DB queries and updates, using the same Cosmos DB container that the PV Agent writes to.'
    level: 200
    duration: 30
    islab: true
---

# D4-M3: MA Agent — Connect Function Tools to Azure Cosmos DB

In this exercise, you'll replace the **in-memory sample data** used by the MA Agent's function tools with real **Azure Cosmos DB** operations. The MA Agent will now read PV requests from and update PV requests in the same `payment-vouchers` container that the PV Agent writes to.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## Learning Objectives

By the end of this exercise, you'll be able to:

1. Connect a Python application to Azure Cosmos DB using `CosmosClient.from_connection_string`
2. Query documents from a Cosmos DB container and filter results in Python
3. Read and replace a document in Cosmos DB to update its fields
4. Verify that the MA Agent's tools interact correctly with the same data the PV Agent produces

## Prerequisites

- Completed **D4-M2** (MA Agent running with two function tools using sample data)
- Completed **D3-M6** (Azure Cosmos DB Serverless account provisioned with `pv-agent-db` database and `payment-vouchers` container)
- At least one PV document submitted by the PV Agent in Cosmos DB (from D3-M6 testing)
- Microsoft Foundry VS Code extension installed and connected
- Python virtual environment activated with packages installed

## Scenario

In the previous exercise, the MA Agent's `get_pv_requests` and `update_pv_approval_status` tools worked against a hardcoded Python list. This was useful for validating the tool pattern and agent behavior, but managers need to see **real data** — the actual PV requests submitted by team members through the PV Agent.

In this exercise, you'll wire both tools to the same Azure Cosmos DB `payment-vouchers` container. After this, the full workflow is connected:

```
PV Agent (D3)                      MA Agent (D4)
    ↓  submit_pv → create_item         ↑  get_pv_requests → query_items
Azure Cosmos DB                         ↑  update_pv_approval_status → replace_item
(payment-vouchers container)  ──────────┘
```

---

## Part 1: Configure the Environment

### Add Cosmos DB variables to `.env`

1. In VS Code, navigate to `labs/03-ma-agent/03-cosmos-db/Labfiles/`.

1. Open `.env.example`. You'll see three Cosmos DB variables alongside the existing project settings:

    ```
    PROJECT_ENDPOINT=your_project_endpoint
    MODEL_DEPLOYMENT_NAME=your_deployment_name
    COSMOS_CONNECTION_STRING=your_cosmos_connection_string
    COSMOS_DATABASE_NAME=pv-agent-db
    COSMOS_CONTAINER_NAME=payment-vouchers
    ```

1. Save a copy as `.env` and fill in all values.

    > **Tip**: The `COSMOS_CONNECTION_STRING` is the same one you used in **D3-M6**. You can find it in the Azure Portal under your Cosmos DB account → **Keys** → **PRIMARY CONNECTION STRING**.

    > **Tip**: `COSMOS_DATABASE_NAME` and `COSMOS_CONTAINER_NAME` should match what you created in D3-M6: `pv-agent-db` and `payment-vouchers`.

---

## Part 2: Set Up the Python Environment

### Configure the Python virtual environment

1. Right-click the `Labfiles/` folder and select **Open in Integrated Terminal**.

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

    > **Note**: The `requirements.txt` for this exercise includes `azure-cosmos>=4.7.0`. This is the Azure Cosmos DB Python SDK.

---

## Part 3: Review the Starter Code

1. Open `ma_agent.py` in the `Labfiles/` folder.

1. Notice that the file already has:
    - The MA Agent instruction (same as D4-M2)
    - Both tool functions (`get_pv_requests` and `update_pv_approval_status`) with **TODO placeholders** where Cosmos DB logic should go
    - The agent creation and conversation loop (same as D4-M2)

1. Your task is to replace the TODO sections with Cosmos DB operations.

---

## Part 4: Add the Cosmos DB Import

1. At the top of `ma_agent.py`, find the existing import block and add the Cosmos DB import:

    ```python
    from azure.cosmos import CosmosClient
    ```

---

## Part 5: Create a Cosmos DB Helper Function

To avoid repeating the connection logic in each tool, create a shared helper function.

1. Below the `MA_AGENT_INSTRUCTIONS` string (after the closing `"""`), add the following helper:

    ```python
    def get_cosmos_container():
        """Helper to get the Cosmos DB container client."""
        cosmos_client = CosmosClient.from_connection_string(os.getenv("COSMOS_CONNECTION_STRING"))
        container = cosmos_client \
            .get_database_client(os.getenv("COSMOS_DATABASE_NAME")) \
            .get_container_client(os.getenv("COSMOS_CONTAINER_NAME"))
        return container
    ```

    Key points:
    - `CosmosClient.from_connection_string(...)` creates a synchronous Cosmos DB client — no separate credential object needed
    - The helper returns the container client, which both tools will use for their respective operations

---

## Part 6: Implement `get_pv_requests` with Cosmos DB Query

1. Find the `get_pv_requests` function. It currently has a TODO placeholder.

1. Replace the **entire function** with the following:

    ```python
    @tool(approval_mode="never_require")
    def get_pv_requests(
        approval_status: Annotated[str, Field(description="The approval status to filter by. Must be exactly 'Pending' or 'Approved'.")]
    ) -> str:
        """Retrieve PV requests filtered by their approval status (Pending or Approved) from Azure Cosmos DB."""
        if approval_status not in ("Pending", "Approved"):
            return f"Invalid approval status '{approval_status}'. Must be 'Pending' or 'Approved'."

        try:
            container = get_cosmos_container()

            # Query all items and filter by approval status
            query = "SELECT * FROM c"
            items = list(container.query_items(query=query, enable_cross_partition_query=True))

            # Filter items by approval status
            filtered = [item for item in items if item.get("approval", {}).get("status") == approval_status]

            if not filtered:
                return f"No PV requests found with approval status '{approval_status}'."

            print(f"\n[Cosmos DB] Found {len(filtered)} PV(s) with status '{approval_status}'\n")
            return json.dumps(filtered, indent=2, ensure_ascii=False)

        except Exception as e:
            print(f"\n[Cosmos DB] Query failed: {e}\n")
            return f"Failed to retrieve PV requests: {str(e)}"
    ```

    Key points:
    - `query_items` with `enable_cross_partition_query=True` fetches all documents across partitions
    - Python-side filtering on `approval.status` keeps the query simple
    - Results are returned as a JSON string for the agent to interpret
    - Errors are caught and returned as strings so the agent can report them gracefully

---

## Part 7: Implement `update_pv_approval_status` with Cosmos DB Read + Replace

1. Find the `update_pv_approval_status` function. It currently has a TODO placeholder.

1. Replace the **entire function** with the following:

    ```python
    @tool(approval_mode="never_require")
    def update_pv_approval_status(
        pv_id: Annotated[str, Field(description="The unique id of the PV request to update")],
        new_status: Annotated[str, Field(description="The new approval status. Must be exactly 'Pending' or 'Approved'.")]
    ) -> str:
        """Update the approval status of a specific PV request by its id in Azure Cosmos DB."""
        if new_status not in ("Pending", "Approved"):
            return f"Invalid status '{new_status}'. Must be 'Pending' or 'Approved'."

        try:
            container = get_cosmos_container()

            # Read the document by id (partition key is /id)
            document = container.read_item(item=pv_id, partition_key=pv_id)

            old_status = document.get("approval", {}).get("status", "Unknown")
            document["approval"]["status"] = new_status

            # Replace the entire document with the updated version
            container.replace_item(item=pv_id, body=document)

            pv_title = document.get("pvTitle", "Unknown")
            print(f"\n[Cosmos DB] PV '{pv_id}' approval status changed: {old_status} → {new_status}\n")
            return f"PV '{pv_id}' ({pv_title}) approval status updated from '{old_status}' to '{new_status}' successfully."

        except Exception as e:
            print(f"\n[Cosmos DB] Update failed: {e}\n")
            return f"Failed to update PV '{pv_id}': {str(e)}"
    ```

    Key points:
    - `read_item(item=pv_id, partition_key=pv_id)` fetches the document using both the item id and the partition key (which is `/id` — the same value)
    - The `approval.status` field is updated in the Python dict
    - `replace_item` sends the modified document back to Cosmos DB, overwriting the existing version
    - The old and new status are logged to the terminal for observability

1. Save the file (**Ctrl+S**).

---

## Part 8: Run the Agent

1. In the terminal (with your virtual environment active), run:

    ```bash
    python ma_agent.py
    ```

    > **Tip**: If you see an authentication error, run `az login` in the terminal and try again.

    > **Tip**: If you see a Cosmos DB connection error, double-check the `COSMOS_CONNECTION_STRING` in your `.env` file.

1. When the agent starts, you should see:

    ```
    MA Agent - Manager Assistant
    ========================================
    MA Agent is ready. Type 'quit' to exit.
    ```

---

## Part 9: Test with Manager Scenarios

> **Important**: Before testing, ensure you have at least one PV document in Cosmos DB from the PV Agent (D3-M6). If your container is empty, go back and run the PV Agent to submit a PV first.

### Scenario 1 — View Pending PVs and Approve One

At the `You:` prompt, enter:

```
Show me all pending PV requests that need my approval.
```

**Expected behavior:**
- Agent calls `get_pv_requests` with `approval_status="Pending"`
- Terminal prints: `[Cosmos DB] Found X PV(s) with status 'Pending'`
- Agent displays a summary of the pending PV(s) — titles, amounts, requestors, etc.

Then follow up in the same session:

```
Please approve the first PV in the list.
```

**Expected behavior:**
- Agent calls `update_pv_approval_status` with the PV's id and `new_status="Approved"`
- Terminal prints: `[Cosmos DB] PV '<id>' approval status changed: Pending → Approved`
- Agent confirms the approval was successful

**Verify in the Portal:**
1. Go to your Cosmos DB account in the Azure Portal
2. Open **Data Explorer** → `pv-agent-db` → `payment-vouchers` → **Items**
3. Find the document by its id — the `approval.status` should now be `"Approved"`

### Scenario 2 — View All Approved PVs

Type `quit` to exit, then restart the agent (`python ma_agent.py`) to start a fresh conversation. At the `You:` prompt, enter:

```
Show me all approved PV requests.
```

**Expected behavior:**
- Agent calls `get_pv_requests` with `approval_status="Approved"`
- Agent displays a summary of all approved PVs, including the one you just approved in Scenario 1
- If no approved PVs exist yet, the agent should report that none were found

> **Observation question**: Does the PV you approved in Scenario 1 now appear in the approved list? This confirms the full read → update → read cycle works end-to-end with Cosmos DB.

1. When you're satisfied with both scenarios, type `quit` to exit.

---

## Deliverables

After completing this exercise, you should have:

- ✅ `.env` configured with `COSMOS_CONNECTION_STRING`, `COSMOS_DATABASE_NAME`, and `COSMOS_CONTAINER_NAME`
- ✅ `azure-cosmos` package installed
- ✅ `get_cosmos_container` helper function implemented
- ✅ `get_pv_requests` wired to `query_items` + Python-side filtering
- ✅ `update_pv_approval_status` wired to `read_item` + `replace_item`
- ✅ Verified the approve flow end-to-end: pending → approved → confirmed in Data Explorer

---

## Summary

In this exercise, you:

- Connected the MA Agent's function tools to the same Azure Cosmos DB container used by the PV Agent
- Implemented `get_pv_requests` using `query_items` to retrieve all documents and filter by approval status in Python
- Implemented `update_pv_approval_status` using `read_item` and `replace_item` to update a document's approval status
- Tested the full manager workflow: view pending requests, approve one, then verify it appears in the approved list

The MA Agent now has a live connection to the PV data store. In the next module, you'll explore how to orchestrate the PV Agent and MA Agent together.
