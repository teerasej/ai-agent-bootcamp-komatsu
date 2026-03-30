---
lab:
    title: 'D3-M6: PV Agent — Store PV Data in Azure Cosmos DB'
    description: 'Provision an Azure Cosmos DB Serverless account, then implement the submit_pv function tool to insert completed PV JSON data into a Cosmos DB container.'
    level: 200
    duration: 45
    islab: true
---

# D3-M6: PV Agent — Store PV Data in Azure Cosmos DB

In this exercise, you'll provision an **Azure Cosmos DB for NoSQL** account using the **Serverless** capacity mode in the Azure Portal, then wire up the `submit_pv` tool in the PV Agent to actually insert each completed Payment Voucher into that database.

This exercise takes approximately **45** minutes.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## Learning Objectives

By the end of this exercise, you'll be able to:

1. Create an Azure Cosmos DB for NoSQL account with Serverless capacity in the Azure Portal
2. Create a database and container with an appropriate partition key
3. Retrieve the Cosmos DB connection string and add it to your environment configuration
4. Use the `azure-cosmos` Python SDK to insert a document from a tool function
5. Verify that the PV document appears in the Data Explorer after submission

## Prerequisites

- Completed **D3-M5** (PV Agent running with `submit_pv` stub, `.env` configured)
- An active [Azure subscription](https://azure.microsoft.com/free/)
- Microsoft Foundry VS Code extension installed and connected
- Python virtual environment activated with packages installed

---

## Part 1: Create an Azure Cosmos DB Serverless Account

Serverless mode bills only for the request units (RUs) your operations consume, with no minimum throughput charge — ideal for development and low-traffic workloads.

### Create the resource

1. Sign in to the [Azure Portal](https://portal.azure.com).

1. In the top search bar, type **Azure Cosmos DB** and select it from the results.

1. Select **+ Create**.

1. On the **Select API option** page, choose **Azure Cosmos DB for NoSQL** and select **Create**.

1. Fill in the **Basics** tab:

   | Field | Value |
   |---|---|
   | **Workload Type** | Learning |
   | **Subscription** | Your Azure subscription |
   | **Resource Group** | Use the same resource group as your Foundry project (e.g., `rg-XX`) |
   | **Account Name** | Enter a globally unique name, e.g., `pv-agent-cosmos-[yourname]` |
   | **Location** | Choose the same region as your Foundry project |
   | **Capacity mode** | Select **Serverless** |

1. Leave all other settings as default and select **Review + create**.

1. Review the configuration and select **Create**. Deployment takes approximately 2–3 minutes.

1. When the deployment is complete, select **Go to resource**.

### Create the database and container

1. In the left navigation of your Cosmos DB account, select **Data Explorer**.

1. Select **New Container**.

1. Fill in the **New Container** panel:

   | Field | Value |
   |---|---|
   | **Database id** | Select **Create new**, enter `pv-agent-db` |
   | **Container id** | `payment-vouchers` |
   | **Partition key** | `/id` |

1. Select **OK**. The new database and container appear in the Data Explorer tree.

### Copy the connection string

1. In the left navigation, select **Keys**.

1. Under **PRIMARY CONNECTION STRING**, select the copy icon to copy the connection string to your clipboard.

   > **Important**: Treat this connection string as a secret — do not commit it to source control.

---

## Part 2: Configure the Environment

1. In VS Code, navigate to `labs/02-pv-agent/06-cosmos-db/Labfiles/`.

1. Open `.env.example`. You'll see three new variables for Cosmos DB alongside the existing ones.

1. Save a copy as `.env` and fill in all five values:

    ```
    PROJECT_ENDPOINT=your_project_endpoint
    MODEL_DEPLOYMENT_NAME=your_deployment_name
    COSMOS_CONNECTION_STRING=your_cosmos_connection_string
    COSMOS_DATABASE_NAME=pv-agent-db
    COSMOS_CONTAINER_NAME=payment-vouchers
    ```

    > **Note**: `COSMOS_DATABASE_NAME` and `COSMOS_CONTAINER_NAME` should match exactly what you entered in the Azure Portal.

---

## Part 3: Install the Cosmos DB Package

1. Right-click the **requirements.txt** file and select **Open in Integrated Terminal**.

1. Activate your virtual environment if it is not already active:
    
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

1. Install the dependencies:

    ```bash
    pip install -r requirements.txt
    ```

    The `azure-cosmos` package is included in `requirements.txt` for this exercise.

---

## Part 4: Implement the `submit_pv` Tool with Cosmos DB Insertion

The `submit_pv` function already exists in `pv_agent.py` — it currently only prints the JSON to the console. You'll replace the body with real Cosmos DB insertion logic.

### Add imports

1. Open `pv_agent.py`.

1. Find the existing import block at the top of the file and add the following two imports:

    ```python
    import json
    import uuid
    from azure.cosmos import CosmosClient
    ```

### Replace the `submit_pv` function body

1. Find the `submit_pv` function. It currently looks like this:

    ```python
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

1. Replace the **body** (everything after the docstring) with the following:

    ```python
    @tool(approval_mode="never_require")
    def submit_pv(
        pv_json: Annotated[str, Field(description="The complete PV JSON object as a string, with status set to ReadyForSubmission and all required fields filled")]
    ) -> str:
        """Insert the completed PV JSON into Azure Cosmos DB."""
        try:
            # Parse the JSON string from the agent
            pv_data = json.loads(pv_json)

            # Extract the inner pv object if wrapped
            document = pv_data.get("pv", pv_data)

            # Cosmos DB requires a unique 'id' field at the document root
            document["id"] = str(uuid.uuid4())

            # Connect to Cosmos DB using the connection string
            cosmos_client = CosmosClient.from_connection_string(os.getenv("COSMOS_CONNECTION_STRING"))
            container = cosmos_client \
                .get_database_client(os.getenv("COSMOS_DATABASE_NAME")) \
                .get_container_client(os.getenv("COSMOS_CONTAINER_NAME"))

            # Insert the document
            container.create_item(body=document)

            print(f"\n[Cosmos DB] Document inserted with id: {document['id']}\n")
            return f"PV submitted successfully and stored in Azure Cosmos DB with id: {document['id']}"

        except Exception as e:
            print(f"\n[Cosmos DB] Insertion failed: {e}\n")
            return f"PV submission failed: {str(e)}"
    ```

    Key points:
    - `CosmosClient.from_connection_string(...)` creates a synchronous client using the connection string — no separate credential object is needed for this approach
    - `pv_data.get("pv", pv_data)` safely unwraps the `pv` wrapper the agent outputs, falling back to the whole object if the wrapper is absent
    - A UUID is assigned as the document `id` to ensure uniqueness across all inserted items
    - The partition key path `/id` matches the UUID, so every insert lands in its own logical partition
    - Errors are caught and returned as a string so the agent can report failures to the user gracefully

1. Save the file (**Ctrl+S**).

---

## Part 5: Run the Agent

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

## Part 6: Test with User Stories

### User Story 1 — Full Data, Successful Submission

```
I need to create a PV for our monthly GitHub Copilot subscription.
The cost is 5,000 baht per month. Project is IT Internal.
My name is Somchai Jaidee. Payee is GitHub Inc.
Purpose is to support developer productivity tools. Approver is Napat Ruangroj.
```

**Expected behavior:**
- Agent collects all fields, calls `get_project_budget` for "IT Internal"
- Agent shows a confirmation summary
- After you confirm, agent calls `submit_pv`
- Terminal prints: `[Cosmos DB] Document inserted with id: <uuid>`
- Agent replies that the PV was stored successfully

**Verify in the Portal:**
1. Go to your Cosmos DB account in the Azure Portal
2. Open **Data Explorer** → `pv-agent-db` → `payment-vouchers` → **Items**
3. You should see the new document with the UUID as its `id`

### User Story 2 — One-Time Equipment Purchase

```
I want to submit a PV to purchase 5 ergonomic chairs for the HR team.
Total cost is 25,000 baht. This is for the HR Internal project.
My name is Wanchai Teeraphon. Payee is Office Furniture Co., Ltd.
Purpose is to improve workspace comfort. Approver is Manee Srisuk.
```

**Expected behavior:**
- Agent identifies `expense.type` as `OneTime` and derives `expense.budgetType` as `"Investment"`
- After confirmation, agent inserts the document into Cosmos DB
- A second document appears in Data Explorer

### User Story 3 — Partial Data, Agent Asks for Missing Fields

```
Please create a PV for a training workshop, 3,500 baht.
```

**Expected behavior:**
- Agent asks for all missing fields one at a time
- After all fields are provided and you confirm, agent calls `submit_pv`
- Document is stored in Cosmos DB

> **Observation question**: Open the inserted document in Data Explorer. Does the document structure match the PV JSON the agent produced? Are `totalBudget` and `remainingBudget` populated with the real values from the CSV?

---

## Deliverables

After completing this exercise, you should have:

- ✅ Azure Cosmos DB for NoSQL Serverless account created with `pv-agent-db` database and `payment-vouchers` container
- ✅ Connection string stored in `.env` as `COSMOS_CONNECTION_STRING`
- ✅ `azure-cosmos` package installed
- ✅ `submit_pv` implemented with `CosmosClient.from_connection_string`, JSON parsing, UUID generation, and `container.create_item`
- ✅ At least one test PV document visible in Data Explorer
