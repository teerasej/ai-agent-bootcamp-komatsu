import os
import json
import asyncio
from dotenv import load_dotenv
from agent_framework.devui import serve
from azure.cosmos import CosmosClient

# Add references
from agent_framework import tool, Agent
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential
from pydantic import Field
from typing import Annotated


# Define MA Agent instructions
MA_AGENT_INSTRUCTIONS = """
You are "MA Agent" — an AI assistant that helps managers review and explore Payment Voucher (PV) requests.

YOUR ROLE:
- Help managers understand and analyze PV request details shared in the conversation
- Summarize PV data clearly and concisely
- Identify missing fields, potential issues, or anomalies in PV data
- Answer questions about PV content based on the data provided
- Highlight important information such as expense amounts, requestors, and approval status
- When the manager asks to see PV requests, call the get_pv_requests tool with the appropriate approval status filter
- When the manager asks to approve or set a PV back to pending, call the update_pv_approval_status tool with the PV id and the new status

UNDERSTANDING PV DATA:
The PV data follows this JSON structure:
{
  "pv": {
    "id": "unique-id",
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
- You do NOT modify PV data directly — always use the available tools
- You do NOT create new PV requests
- If PV data is incomplete, note which fields are missing but do not fill them in
- Always base your analysis on the exact data provided — do not invent or assume unreported values
- When updating approval status, you MUST use the exact PV id from the data returned by get_pv_requests
"""


def get_cosmos_container():
    """Helper to get the Cosmos DB container client."""
    cosmos_client = CosmosClient.from_connection_string(os.getenv("COSMOS_CONNECTION_STRING"))
    container = cosmos_client \
        .get_database_client(os.getenv("COSMOS_DATABASE_NAME")) \
        .get_container_client(os.getenv("COSMOS_CONTAINER_NAME"))
    return container


# Create a tool function to get PV requests by approval status
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


# Create a tool function to update approval status of a PV request
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


async def main():
    # Clear the console
    os.system('cls' if os.name == 'nt' else 'clear')

    # Load environment variables
    load_dotenv()

    print("MA Agent - Manager Assistant")
    print("=" * 40)
    print("Type 'quit' to exit\n")

    # Initialize a credential
    credential = AzureCliCredential()

    # Create the agent and start a conversation loop
    async with Agent(
        client=AzureOpenAIResponsesClient(
            credential=credential,
            deployment_name=os.getenv("MODEL_DEPLOYMENT_NAME"),
            project_endpoint=os.getenv("PROJECT_ENDPOINT"),
        ),
        instructions=MA_AGENT_INSTRUCTIONS,
        tools=[get_pv_requests, update_pv_approval_status],
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


if __name__ == "__main__":
    agent = asyncio.run(main())
    # serve(entities=[agent], auto_open=True)
