import os
import asyncio
from dotenv import load_dotenv

# Add references
from agent_framework import Agent
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential
from agent_framework.devui import serve


async def main():
    # Load environment variables
    load_dotenv()
    project_endpoint = os.getenv("PROJECT_ENDPOINT")
    model_deployment = os.getenv("MODEL_DEPLOYMENT_NAME")

    print("Testing connection to Foundry project...")
    print(f"Endpoint: {project_endpoint}")
    print(f"Model: {model_deployment}\n")

    # Create and run the agent to test the connection
    async with Agent(
        client=AzureOpenAIResponsesClient(
            credential=AzureCliCredential(),
            deployment_name=model_deployment,
            project_endpoint=project_endpoint,
        ),
        instructions="You are a helpful assistant.",
    ) as agent:
        response = await agent.run(
            ["Hello! Are you available? Please confirm you are working correctly."]
        )

        # Print the response and confirm success
        print(f"Agent response: {response}")
        print("\nSmoke test PASSED: Agent is working correctly!")

    return agent


if __name__ == "__main__":
    agent = asyncio.run(main())
    serve(entities=[agent], auto_open=True)
