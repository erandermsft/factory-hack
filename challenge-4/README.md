1. Install ASPIRE
curl -fsSL https://aspire.dev/install.sh | bash -s -- --skip-path

2. aspire new aspire-py-starter

3. curl -LsSf https://astral.sh/uv/install.sh | sh

4. Install rest proxy VSCODE extension

## Workflow configuration

The Challenge-4 workflow expects the Anomaly + Fault agents to be hosted in Azure AI Foundry Agent Service.
Set the following environment variables (for example via a `.env` file in `challenge-4/agent-workflow/app/`):

- `AZURE_AI_PROJECT_ENDPOINT` – your Foundry project endpoint
- `ANOMALY_AGENT_ID` – Agent Service ID for the hosted AnomalyDetectionAgent
- `FAULT_DIAGNOSIS_AGENT_ID` – Agent Service ID for the hosted FaultDiagnosisAgent
- `REPAIR_PLANNER_AGENT_URL` – (optional) A2A base URL for the RepairPlanner agent