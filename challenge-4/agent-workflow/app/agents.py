from dotenv import load_dotenv
from agent_framework import WorkflowBuilder, Executor, handler, WorkflowContext

import os
import sys
import logging
from contextlib import AsyncExitStack
from typing import Any
from agent_framework.a2a import A2AAgent
from agent_framework import ChatAgent

# Add challenge-1/agents to Python path to import inplace agents
# Get absolute path to workspace root (parent of challenge-4)
workspace_root = os.path.dirname(os.path.dirname(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
challenge1_agents_path = os.path.join(workspace_root, "challenge-1", "agents")
if challenge1_agents_path not in sys.path:
    sys.path.append(challenge1_agents_path)
import fault_diagnosis_agent
import anomaly_classification_agent
# Import agents from challenge-1
# Note: These imports might trigger top-level code execution in those modules


load_dotenv(override=True)
logger = logging.getLogger(__name__)


async def get_a2a_agent(server_url: str) -> ChatAgent:
    """Create and return an A2A ChatAgent connected to the specified server URL."""
    import httpx
    from a2a.client import A2ACardResolver

    # Create httpx client for HTTP communication
    async with httpx.AsyncClient(timeout=60.0) as http_client:
        resolver = A2ACardResolver(httpx_client=http_client, base_url=server_url)
        agent_card = await resolver.get_agent_card(relative_card_path=".well-known/agent-card.json")

        # Create A2A agent instance
        agent = A2AAgent(
            name=agent_card.name,
            description=agent_card.description,
            agent_card=agent_card,
            url=server_url)

    # agent = A2AAgent(
    #     name="My A2A Agent",
    #     description="A directly configured A2A agent",
    #     url=server_url + "/echo"
    # )

    return agent


def extract_text_from_message(msg: Any) -> str:
    """Helper to extract text from various message types used in the workflow."""
    text = ""
    # Priority 1: Check for AgentExecutorResponse used by framework workflows
    if hasattr(msg, 'agent_run_response') and hasattr(msg.agent_run_response, 'text'):
        text = msg.agent_run_response.text
    # Priority 2: Direct text attribute
    elif getattr(msg, 'text', None):
        text = msg.text
    # Priority 3: Nested response (e.g. wrapper)
    elif getattr(msg, 'response', None) and getattr(msg.response, 'text', None):
        text = msg.response.text
    # Priority 4: Event parameters
    elif getattr(msg, 'params', None):
        params = msg.params
        if isinstance(params, dict):
            text = params.get('text', '') or str(params)
        elif hasattr(params, 'text'):
            text = params.text
        else:
            text = str(params)
    # Priority 5: Fallback string representation
    else:
        text = str(msg)
    return text

# --- Workflow Executors ---


class RequestProcessor(Executor):
    @handler
    async def process(self, data: dict, ctx: WorkflowContext[str]) -> None:
        machine_id = data.get("machine_id")
        telemetry = data.get("telemetry")
        # Format the initial prompt for the Anomaly Agent
        prompt = f'Classify the following anomalies for machine {machine_id}: {telemetry}'
        await ctx.send_message(prompt)


class FaultAgentExecutor(Executor):
    """
    Wraps the Azure AI Projects (Foundry) Agent from Challenge 1.
    Since it uses a different SDK (azure.ai.projects), we adapt it to the Agent Framework Executor.
    """

    def __init__(self, project_client, agent_definition, id: str = None):
        super().__init__(id=id)
        self.project_client = project_client
        self.agent_def = agent_definition
        self.openai_client = None

    @handler
    async def process(self, message: Any, ctx: WorkflowContext[str]) -> None:
        # Initialize OpenAI client on first use to avoid blocking init if not needed
        if not self.openai_client:
            self.openai_client = self.project_client.get_openai_client()

        try:
            # We create a new conversation for each turn here to keep it simple,
            # or we could manage conversation ID in the context if multi-turn was needed.
            conversation = self.openai_client.conversations.create()

            # The message from the previous agent (Anomaly) is passed as input
            text_message = extract_text_from_message(message)
            input_text = f"Context from Anomaly Agent: {text_message}\n\nPlease diagnose the root cause."

            response = self.openai_client.responses.create(
                conversation=conversation.id,
                input=input_text,
                extra_body={"agent": {"name": self.agent_def.name,
                                      "type": "agent_reference"}},
            )

            # Send the result back to the workflow
            # Use send_message for Executor-to-Executor communication
            await ctx.send_message(response.output_text)
            # Or use yield_output if this is the final result?
            # The workflow will capture outputs from all agents if configured,
            # but usually we want to explicitely yield final output or let the caller inspect the trace/messages.
            # Here we just pass it along.

        except Exception as e:
            logger.error(f"Fault Agent failed: {e}")
            await ctx.send_message(f"Fault Agent encountered an error: {str(e)}")


def diagnosis_condition(msg) -> bool:
    """Determine if Fault Diagnosis is needed based on Anomaly Agent output."""
    logger.info(f"Evaluating diagnosis condition on message type: {type(msg)}")

    text = extract_text_from_message(msg)

    logger.info(f"Diagnosis text extracted: {text[:200]}...")

    keywords = ["critical", "warning", "high", "alert"]
    should_run = any(keyword in text.lower() for keyword in keywords)
    logger.info(f"Diagnosis condition result: {should_run}")
    return should_run

# --- Main Workflow Function ---


async def run_factory_workflow(machine_id: str, telemetry: list):
    """
    Creates and runs the Factory Analysis Workflow reusing Challenge 1 agents.
    """

    # 1. Create Challenge 1 Agents "Inplace"
    # Anomaly Agent (Agent Framework compatible)
    # Note: create_agent returns (client, agent). We need to manage client lifecycle.
    anom_client, anom_agent = await anomaly_classification_agent.create_agent()

    # Fault Agent (Azure AI Projects / Foundry)
    # create_agent returns (project_client, agent_def)
    fault_project_client, fault_agent_def = fault_diagnosis_agent.create_agent()

    repair_planner_agent = await get_a2a_agent(server_url=os.getenv("REPAIR_PLANNER_AGENT_URL"))

    try:
        # Build the workflow
        logger.info("Building workflow with inplace agents...")
        builder = WorkflowBuilder()

        # Register Executors
        builder.register_executor(lambda: RequestProcessor(
            id="init"), name="RequestProcessor")

        # Register Anomaly Agent directly (compatible)
        builder.register_agent(
            lambda: anom_agent, name="AnomalyAgent", output_response=True)

        builder.register_agent(lambda: repair_planner_agent,
                               name="RepairPlannerAgent", output_response=True)

        # Register Fault Agent via Wrapper
        # We need a lambda that returns a new instance of our custom executor
        builder.register_executor(
            lambda: FaultAgentExecutor(
                fault_project_client, fault_agent_def, id="FaultExecutor"),
            name="FaultAgent"
        )

        builder.set_start_executor("RequestProcessor")

        # Edge 1: Request -> Anomaly
        builder.add_edge("RequestProcessor", "AnomalyAgent")

        # Edge 2: Anomaly -> Fault (Conditional)
        builder.add_edge("AnomalyAgent", "FaultAgent",
                         condition=diagnosis_condition)

        builder.add_edge("FaultAgent", "RepairPlannerAgent")

        workflow = builder.build()

        # Execute
        result = await workflow.run({"machine_id": machine_id, "telemetry": telemetry})

        return result.get_outputs()

    finally:
        # Cleanup
        await anom_client.close()
        try:
            fault_project_client.close()
        except:
            pass  # It might not be async or closeable in the same way, but good practice
