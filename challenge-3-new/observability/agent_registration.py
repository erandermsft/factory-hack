"""Agent registration in Azure AI Foundry portal."""
from datetime import datetime
from azure.ai.projects.aio import AIProjectClient
from azure.identity.aio import DefaultAzureCredential


async def register_agent(
    project_endpoint: str,
    agent_name: str,
    deployment_name: str,
    instructions: str,
    description: str = ""
) -> bool:
    """Register agent in Azure AI Foundry portal.
    
    Args:
        project_endpoint: Azure AI Foundry project endpoint
        agent_name: Name of the agent
        deployment_name: Model deployment name
        instructions: Agent instructions/system prompt
        description: Agent description
        
    Returns:
        True if registration successful, False otherwise
    """
    credential = DefaultAzureCredential()
    
    async with AIProjectClient(endpoint=project_endpoint, credential=credential) as project_client:
        try:
            from azure.ai.projects.models import PromptAgentDefinition
            
            # Get current agent version from portal
            print(f"   Checking existing agent versions for '{agent_name}' in portal...")
            version_count = 0
            try:
                async for version_obj in project_client.agents.list_versions(agent_name=agent_name):
                    version_count += 1
                print(f"   Found {version_count} existing versions")
            except Exception as e:
                print(f"   Error checking versions: {e}")
            
            print(f"   Creating new version (will be version #{version_count + 1})...")
            
            # Create agent definition
            definition = PromptAgentDefinition(
                model=deployment_name,
                instructions=instructions,
            )
            
            # Create new version - Azure auto-assigns version number
            print(f"   Registering {agent_name} in Azure AI Foundry portal...")
            registered_agent = await project_client.agents.create_version(
                agent_name=agent_name,
                definition=definition,
                description=description or f"{agent_name} agent",
                metadata={
                    "framework": "agent-framework",
                    "purpose": "maintenance_scheduling",
                    "timestamp": datetime.utcnow().isoformat()
                }
            )
            print(f"   ✅ New version created!")
            print(f"      Agent ID: {registered_agent.id if hasattr(registered_agent, 'id') else 'N/A'}")
            
            # Verify it was created
            print(f"   Verifying creation...")
            verify_count = 0
            async for v in project_client.agents.list_versions(agent_name=agent_name):
                verify_count += 1
            print(f"   Total versions now in portal: {verify_count}")
            print(f"   Check portal at: https://ai.azure.com\n")
            
            return True
        except Exception as e:
            print(f"   ⚠️  Could not register agent in portal: {e}\n")
            return False
