import os
import asyncio
from dotenv import load_dotenv

# Add references
from agent_framework import Agent
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential


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
