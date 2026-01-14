from dotenv import load_dotenv
from agent_framework import WorkflowBuilder, Executor, handler, WorkflowContext

import os
import logging
from typing import Any
from agent_framework import ChatAgent
from agent_framework.azure import AzureAIAgentClient
from azure.identity.aio import DefaultAzureCredential

load_dotenv(override=True)
logger = logging.getLogger(__name__)


def _require_env(name: str) -> str:
    value = os.getenv(name)
    if not value:
        raise RuntimeError(
            f"Missing required environment variable: {name}. "
            "This workflow expects Anomaly/Fault agents to be hosted in Foundry Agent Service and referenced by ID."
        )
    return value


async def get_a2a_agent(server_url: str) -> ChatAgent:
    """Create and return an A2A ChatAgent connected to the specified server URL."""
    try:
        from agent_framework.a2a import A2AAgent
        import importlib

        a2a = importlib.import_module("agent_framework_a2a")
    except ModuleNotFoundError as e:
        raise RuntimeError(
            "A2A support requires the 'agent-framework-a2a' package. "
            "If you're using uv, run `uv sync` in this directory."
        ) from e

    resolver_cls = getattr(a2a, "A2ACardResolver", None)
    if resolver_cls is not None:
        import httpx

        async with httpx.AsyncClient(timeout=60.0) as http_client:
            resolver = resolver_cls(httpx_client=http_client, base_url=server_url)
            agent_card = await resolver.get_agent_card(relative_card_path=".well-known/agent-card.json")

        return A2AAgent(
            name=agent_card.name,
            description=getattr(agent_card, "description", ""),
            agent_card=agent_card,
            url=server_url,
        )

    # Fallback: older/newer A2A packages may not ship a card resolver.
    # Try the most common constructor shapes.
    for kwargs in (
        {"url": server_url},
        {"name": "RepairPlannerAgent", "description": "A2A agent", "url": server_url},
    ):
        try:
            return A2AAgent(**kwargs)
        except TypeError:
            continue

    raise RuntimeError(
        "Unable to construct A2A agent from 'agent-framework-a2a'. "
        "Please ensure the package version matches the workflow sample."
    )


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
    Creates and runs the Factory Analysis Workflow.

    AnomalyDetectionAgent + FaultDiagnosisAgent are hosted in Foundry Agent Service.
    We reference them by Agent ID; any tool/MCP calls happen server-side in the managed service.
    """

    project_endpoint = _require_env("AZURE_AI_PROJECT_ENDPOINT")
    #anomaly_agent_id = _require_env("ANOMALY_AGENT_ID")
   # fault_agent_id = _require_env("FAULT_DIAGNOSIS_AGENT_ID")
    anomaly_agent_id = "AnomalyClassificationAgent"
    fault_agent_id="FaultDiagnosisAgent"
    repair_planner_url = os.getenv("REPAIR_PLANNER_AGENT_URL")

    credential = DefaultAzureCredential()
    try:
        async with AzureAIAgentClient(
            project_endpoint=project_endpoint,
            credential=credential,
            agent_id=anomaly_agent_id,
            should_cleanup_agent=False,
        ) as anomaly_client, AzureAIAgentClient(
            project_endpoint=project_endpoint,
            credential=credential,
            agent_id=fault_agent_id,
            should_cleanup_agent=False,
        ) as fault_client:
            anomaly_agent = anomaly_client.create_agent(name="AnomalyClassificationAgent")
            fault_agent = fault_client.create_agent(name="FaultDiagnosisAgent")

            # Build the workflow
            logger.info("Building workflow with hosted Foundry agents by ID...")
            builder = WorkflowBuilder()

            builder.register_executor(lambda: RequestProcessor(id="init"), name="RequestProcessor")
            builder.register_agent(lambda: anomaly_agent, name="AnomalyAgent", output_response=True)
            builder.register_agent(lambda: fault_agent, name="FaultAgent", output_response=True)

            if repair_planner_url:
                repair_planner_agent = await get_a2a_agent(server_url=repair_planner_url)
                builder.register_agent(
                    lambda: repair_planner_agent, name="RepairPlannerAgent", output_response=True
                )

            builder.set_start_executor("RequestProcessor")
            builder.add_edge("RequestProcessor", "AnomalyAgent")
            builder.add_edge("AnomalyAgent", "FaultAgent", condition=diagnosis_condition)

            if repair_planner_url:
                builder.add_edge("FaultAgent", "RepairPlannerAgent")

            workflow = builder.build()
            result = await workflow.run({"machine_id": machine_id, "telemetry": telemetry})
            return result.get_outputs()
    finally:
        await credential.close()
