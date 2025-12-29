import asyncio
import os
import importlib.util
from pathlib import Path
from typing import Annotated
from agent_framework import HostedMCPTool
from azure.identity.aio import AzureCliCredential
from agent_framework import ChatAgent
from agent_framework.azure import AzureAIAgentClient, AzureAIClient
from azure.ai.projects.aio import AIProjectClient

from azure.cosmos import CosmosClient
from pydantic import Field
from dotenv import load_dotenv
from azure.ai.agents.models import (
    ListSortOrder,
    McpTool,
    RequiredMcpToolCall,
    RunStepActivityDetails,
    SubmitToolApprovalAction,
    ToolApproval,
)
load_dotenv(override=True)

# Configuration
project_endpoint = os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
model_deployment_name = os.environ.get("MODEL_DEPLOYMENT_NAME")
sc_connection_id = os.environ.get("AZURE_AI_CONNECTION_ID")
cosmos_endpoint = os.environ.get("COSMOS_ENDPOINT")
cosmos_key = os.environ.get("COSMOS_KEY")

# Initialize Cosmos DB clients globally for function tools
cosmos_client = CosmosClient(cosmos_endpoint, cosmos_key)
database = cosmos_client.get_database_client("FactoryOpsDB")
knowledge_container = database.get_container_client("KnowledgeBase")
machines_container = database.get_container_client("Machines")
mcp_endpoint = os.environ.get("MACHINE_MCP_SERVER_ENDPOINT")
mcp_subscription_key = os.environ.get("APIM_SUBSCRIPTION_KEY")

def get_knowledge_data(machine_type: str) -> dict:
    """Get all knowledge base information for a machine type from Cosmos DB"""
    try:
        query = f"SELECT * FROM c WHERE c.machineType = '{machine_type}'"
        items = list(knowledge_container.query_items(
            query=query,
            enable_cross_partition_query=True
        ))
        return items
    except Exception as e:
        return {"error": str(e)}


def get_machine_data(machine_id: str) -> dict:
    """Get machine data from Cosmos DB"""
    try:
        query = f"SELECT * FROM c WHERE c.id = '{machine_id}'"
        items = list(machines_container.query_items(
            query=query,
            enable_cross_partition_query=True
        ))
        return items[0] if items else {"error": f"Machine {machine_id} not found"}
    except Exception as e:
        return {"error": str(e)}


async def main():
    try:
        async with (
            AzureCliCredential() as credential,
            AzureAIClient(credential=credential).create_agent(
                    name="FaultDiagnosisAgent",
                    instructions="""You are a Fault Diagnosis Agent evaluating the root cause of maintenance alerts
                        You will receive detected sensor deviations for a given machine. Your task is to:
                        - Find the most likely root cause for the deviation 

                        You have access to the following tools:
                        - get_knowledge_data: fetch knowledge base information for possible causes 
                        - get_machine_data: fetch machine information such as maintenance history and type for a particular machine id

                        Use these functions to determnine the root cause for each alert
                        

                        Output should be a summary of the most likely root cause
                

                        """,
                    tools = [get_machine_data, get_knowledge_data]

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

                    #   return agent

    except Exception as e:
        print(f"❌ Error creating agent: {e}")
        print("Make sure you have run 'az login' and have proper Azure credentials configured.")
        return None

if __name__ == "__main__":
    asyncio.run(main())
