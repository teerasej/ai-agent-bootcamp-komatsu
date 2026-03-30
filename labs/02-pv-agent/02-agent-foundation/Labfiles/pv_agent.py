import os
import asyncio
from dotenv import load_dotenv
from agent_framework.devui import serve

# Add references


# Define PV Agent instructions
PV_AGENT_INSTRUCTIONS = """
# Replace this with your PV Agent instruction
"""


async def main():
    # Clear the console
    os.system('cls' if os.name == 'nt' else 'clear')

    # Load environment variables
    load_dotenv()

    print("PV Agent - Payment Voucher Assistant")
    print("=" * 40)
    print("Type 'quit' to exit\n")

    # Initialize a credential


    # Create the agent and start a conversation loop



    

if __name__ == "__main__":
    agent = asyncio.run(main())
