import os
import json
import asyncio
from dotenv import load_dotenv
from agent_framework.devui import serve
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
        conversation_history = []

        # return agent

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
