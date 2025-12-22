# import asyncio
# import os
# from agent_framework import ChatAgent
# from agent_framework.azure import AzureAIClient
# from azure.ai.projects.aio import AIProjectClient
# from azure.identity.aio import AzureCliCredential
# from agent_framework import HostedWebSearchTool, HostedMCPTool

# async def main():
#     async with (
#         AzureCliCredential() as credential,
#         AIProjectClient(
#             endpoint=os.environ["AZURE_FOUNDRY_PROJECT_ENDPOINT"],  # or AZURE_AI_PROJECT_ENDPOINT
#             credential=credential
#         ) as project_client,
#             AzureAIClient(
#                 project_client=project_client,
#                 agent_name="PersistentAgent",
#                 model_deployment_name=os.environ["AZURE_FOUNDRY_MODEL_DEPLOYMENT_NAME"],  # or AZURE_AI_MODEL_DEPLOYMENT_NAME
#                 instructions="You are a document assistant v2",
#                 tools=[
#                     HostedMCPTool(
#                         name="Microsoft Learn MCP",
#                         url="https://learn.microsoft.com/api/mcp"
#                     )
#                 ]
#                     ).create_agent() as agent,
#             ):

#         result = await agent.run("How do I create an Azure storage account?")
#         print(result.text)

# asyncio.run(main())

# import asyncio
# from agent_framework.azure import AzureAIClient
# from azure.identity.aio import AzureCliCredential

# async def main():
#     async with (
#         AzureCliCredential() as credential,
#         AzureAIClient(credential=credential).create_agent(
#             instructions="You are very good at telling jokes. V3 agent",
#             name='ScratchPadAgent2',

#         ) as agent,
#     ):
#         result = await agent.run("Tell me a joke about a pirate.")
#         print(result.text)

# if __name__ == "__main__":
#     asyncio.run(main())
# import asyncio
# from agent_framework.azure import AzureAIAgentClient
# from azure.identity.aio import AzureCliCredential

# async def main():
#     async with (
#         AzureCliCredential() as credential,
#         AzureAIAgentClient(credential=credential).create_agent(
#             name="HelperAgent",
#             instructions="You are a helpful assistant."
#         ) as agent,
#     ):
#         result = await agent.run("Hello!")
#         print(result.text)

# asyncio.run(main())
# import asyncio
# import os
# from agent_framework import ChatAgent
# from agent_framework.azure import AzureAIAgentClient
# from azure.ai.projects.aio import AIProjectClient
# from azure.identity.aio import AzureCliCredential

# async def main():
#     async with (
#         AzureCliCredential() as credential,
#         AIProjectClient(
#             endpoint=os.environ["AZURE_AI_PROJECT_ENDPOINT"],
#             credential=credential
#         ) as project_client,
#     ):
#         # Create a persistent agent
#         created_agent = await project_client.agents.create_agent(
#             model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
#             name="PersistentAgent",
#             instructions="You are a helpful assistant."
#         )

#         try:
#             # Use the agent
#             async with ChatAgent(
#                 chat_client=AzureAIAgentClient(
#                     project_client=project_client,
#                     agent_id=created_agent.id
#                 ),
#                 instructions="You are a helpful assistant."
#             ) as agent:
#                 result = await agent.run("Hello!")
#                 print(result.text)
#         finally:
#             # Clean up the agent
#             await project_client.agents.delete_agent(created_agent.id)

# asyncio.run(main())