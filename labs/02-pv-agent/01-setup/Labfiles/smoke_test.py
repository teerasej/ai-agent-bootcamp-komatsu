import os
import asyncio
from dotenv import load_dotenv

# Add references


async def main():
    # Load environment variables
    load_dotenv()
    project_endpoint = os.getenv("PROJECT_ENDPOINT")
    model_deployment = os.getenv("MODEL_DEPLOYMENT_NAME")

    print("Testing connection to Foundry project...")
    print(f"Endpoint: {project_endpoint}")
    print(f"Model: {model_deployment}")


    # Create and run the agent to test the connection


    # Print the response and confirm success


if __name__ == "__main__":
    asyncio.run(main())
