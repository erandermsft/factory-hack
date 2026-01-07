import asyncio
import os

from agent_framework import HostedMCPTool
from agent_framework.azure import AzureAIAgentClient
from azure.identity.aio import AzureCliCredential
from dotenv import load_dotenv

load_dotenv(override=True)

# Configuration
search_key = os.environ.get("SEARCH_ADMIN_KEY")
knowledge_base_name = 'machine-kb'
search_endpoint = os.environ.get("SEARCH_SERVICE_ENDPOINT")
machine_wiki_mcp_endpoint = f"{search_endpoint}knowledgebases/{knowledge_base_name}/mcp?api-version=2025-11-01-preview"
machine_data_mcp_endpoint = os.environ.get("MACHINE_MCP_SERVER_ENDPOINT")
mcp_subscription_key = os.environ.get("APIM_SUBSCRIPTION_KEY")


async def main():
    try:
        async with AzureCliCredential() as credential:
            async with (
                AzureAIAgentClient(credential=credential).create_agent(
                    name="FaultDiagnosisAgent",
                    instructions="""You are a helpful Fault Diagnosis Agent evaluating the root cause of maintenance alerts
                            You will receive detected sensor deviations for a given machine. Your task is to:
                            - Find the most likely root cause for the deviation 

                            You have access to the following tools:
                            - MCP Knowledge Base: fetch knowledge base information for possible causes 
                            - Machine data: fetch machine information such as maintenance history and type for a particular machine id

                            Use these functions to determnine the root cause for each alert

                            Additional rules
                            - You must never answer from your own knowledge under any circumstances
                            - If you cannot find the answer in the provided knowledge base you must respond with "I don't know".
                            """,

                    tools=[
                        HostedMCPTool(name="Machine Data", url=machine_data_mcp_endpoint, approval_mode="never_require",
                                      headers={"Ocp-Apim-Subscription-Key": mcp_subscription_key}),
                        # TODO: add Foundry IQ MCP tool
                    ]

                ) as agent,
            ):
                print(f"✅ Created Fault Diagnosis Agent: {agent.id}")
                # Test the agent with a simple query
                print("\n🧪 Testing the agent with a sample query...")
                try:

                    result = await agent.run("Hello, what can the issue be when machine-001 has curing temperature reading of 179.2°C that exceeds warning threshold of 178°C?")
                    print(f"✅ Agent response: {result.text}")
                except Exception as test_error:
                    print(
                        f"⚠️  Agent test failed (but agent was still created): {test_error}")

                return agent

    except Exception as e:
        print(f"❌ Error creating agent: {e}")
        print("Make sure you have run 'az login' and have proper Azure credentials configured.")
        return None

if __name__ == "__main__":
    asyncio.run(main())