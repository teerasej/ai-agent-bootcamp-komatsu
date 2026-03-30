import os
import csv
import asyncio
from dotenv import load_dotenv

# Add references
from agent_framework import tool, Agent
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential
from pydantic import Field
from typing import Annotated


# Define PV Agent instructions
PV_AGENT_INSTRUCTIONS = """
You are "PV Agent" — an AI assistant that helps requestors draft a Payment Voucher (PV) request.

YOUR ROLE:
- Guide the user through collecting all required PV fields via natural conversation
- Ask only for missing critical fields, one at a time
- State your assumptions explicitly (for example, "I'll use THB as the default currency")
- Show a confirmation summary before marking the PV as ReadyForSubmission
- Always output a valid JSON object as the final step
- When the user provides a project name, call the get_project_budget tool to look up the project's budget and remaining budget. Use this data to populate project.budgetSummary in the output JSON.

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
- expense.budgetType: MUST be exactly "Expense" or "Investment" — derive this from context:
    - Use "Expense" for recurring or operational costs (subscriptions, monthly fees, training, consumables)
    - Use "Investment" for one-time capital expenditures that create lasting value (equipment, tool licenses, infrastructure)
    - If the user's intent is ambiguous, infer from expense.type: MonthlyFee → lean toward "Expense", OneTime → consider both and ask if still unclear
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
      "budgetType": "Expense | Investment",
      "amount": { "value": 0, "currency": "THB" }
    },
    "project": {
      "projectName": "...",
      "budgetSummary": {
        "totalBudget": 0,
        "remainingBudget": 0
      }
    },
    "approval": { "approverName": "...", "status": "Pending" },
    "status": "Draft | ReadyForSubmission"
  }
}
"""


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


async def main():
    # Clear the console
    os.system('cls' if os.name == 'nt' else 'clear')

    # Load environment variables
    load_dotenv()

    print("PV Agent - Payment Voucher Assistant")
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
        instructions=PV_AGENT_INSTRUCTIONS,
        tools=[get_project_budget],
    ) as agent:
        conversation_history = []

        print("PV Agent is ready. Type 'quit' to exit.\n")

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
    asyncio.run(main())
