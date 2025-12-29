from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition, MCPTool
from azure.identity import DefaultAzureCredential
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
import os
import requests
# Provide agent configuration details
credential = DefaultAzureCredential()

knowledge_base_name = 'machine-kb'
search_endpoint = os.environ.get("SEARCH_SERVICE_ENDPOINT")
mcp_endpoint = f"{search_endpoint}knowledgebases/{knowledge_base_name}/mcp?api-version=2025-11-01-preview"
project_endpoint = os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
project_connection_name = "machine-wiki-connection"
agent_name = "kbtestagent"
agent_model = "gpt-4.1" # e.g. gpt-4.1-mini

# Create project client
project_client = AIProjectClient(endpoint = project_endpoint, credential = credential)

# Define agent instructions (see "Optimize agent instructions" section for guidance)
instructions = """
You are a helpful assistant that must use the knowledge base to answer all the questions from user. You must never answer from your own knowledge under any circumstances.
Every answer must always provide annotations for using the MCP knowledge base tool and render them as: `【message_idx:search_idx†source_name】`
If you cannot find the answer in the provided knowledge base you must respond with "I don't know".
"""

# Create MCP tool with knowledge base connection
mcp_kb_tool = MCPTool(
    server_label = "knowledge-base",
    server_url = mcp_endpoint,
    require_approval = "never",
    allowed_tools = ["knowledge_base_retrieve"],
    project_connection_id = project_connection_name
)

# Create agent with MCP tool
agent = project_client.agents.create_version(
    agent_name = agent_name,
    definition = PromptAgentDefinition(
        model = agent_model,
        instructions = instructions,
        tools = [mcp_kb_tool]
    )
)

print(f"Agent '{agent_name}' created or updated successfully.")
# Get the OpenAI client for responses and conversations
openai_client = project_client.get_openai_client()

conversation = openai_client.conversations.create()

bearer_token_provider = get_bearer_token_provider(
        credential, "https://management.azure.com/.default")

token = bearer_token_provider()
print(token)
response = requests.post(
mcp_endpoint,
headers={
"Authorization": f"Bearer {token}",
"Accept": "application/json, text/event-stream" # Required header
},
json={"jsonrpc": "2.0", "id": 1, "method": "tools/list"}
)
print(response)
# Send initial request that will trigger the MCP tool
response = openai_client.responses.create(
    conversation=conversation.id,
    tool_choice="required",
    input="""
        List something from the knowledge base 
    """,
    extra_body={"agent": {"name": agent.name, "type": "agent_reference"}},
)

print(f"Response: {response.output_text}")