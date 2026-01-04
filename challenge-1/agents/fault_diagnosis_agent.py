import asyncio
import os

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import MCPTool, PromptAgentDefinition
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

load_dotenv(override=True)

# Configuration
project_endpoint = os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
model_deployment_name = os.environ.get("MODEL_DEPLOYMENT_NAME")
knowledge_base_name = 'machine-kb'
search_endpoint = os.environ.get("SEARCH_SERVICE_ENDPOINT")

machine_wiki_mcp_endpoint = f"{search_endpoint}knowledgebases/{knowledge_base_name}/mcp?api-version=2025-11-01-preview"
project_wiki_connection_name = "machine-wiki-connection"

machine_data_mcp_endpoint = os.environ.get("MACHINE_MCP_SERVER_ENDPOINT")
machine_data_subscription_key = os.environ.get("APIM_SUBSCRIPTION_KEY")


mcp_kb_tool = MCPTool(
    server_label="knowledge_base",
    server_url=machine_wiki_mcp_endpoint,
    require_approval="never",
    allowed_tools=["knowledge_base_retrieve"],
    project_connection_id=project_wiki_connection_name
)
mcp_machine_data_tool = MCPTool(
    server_label="machine_data",
    server_url=machine_data_mcp_endpoint,
    require_approval="never",
    headers={"Ocp-Apim-Subscription-Key": machine_data_subscription_key}
)


async def main():
    try:
        credential = DefaultAzureCredential()

        project_client = AIProjectClient(
            endpoint=project_endpoint, credential=credential)

        agent = project_client.agents.create_version(
            agent_name="FaultDiagnosisAgent",
            definition=PromptAgentDefinition(
                model=model_deployment_name,
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
                tools=[mcp_machine_data_tool, mcp_kb_tool]


            ))
        print(f"✅ Created Fault Diagnosis Agent: {agent.id}")
        # Test the agent with a simple query
        print("\n🧪 Testing the agent with a sample query...")
        try:
            # Get the OpenAI client for responses and conversations
            openai_client = project_client.get_openai_client()

            # Create conversation
            conversation = openai_client.conversations.create()
            # Send request to trigger the MCP tool
            response = openai_client.responses.create(
                conversation=conversation.id,
                input="""
                    Hello, what can the issue be when machine-001 has curing temperature reading of 179.2°C that exceeds warning threshold of 178°C?
                """,
                extra_body={"agent": {"name": agent.name,
                                      "type": "agent_reference"}},
            )
            print(f"✅ Agent response: {response.output_text}")
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
