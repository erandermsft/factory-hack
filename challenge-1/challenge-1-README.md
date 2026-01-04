# Challenge 1: Agent Framework Agents for Anomaly Classification and Fault Diagnosis

**Expected Duration**: 60 min

Welcome to Challenge 1!

In this challenge, we will build two specialized agents for classifying and understanding machine anomalies. We'll develop a **Anomaly Classification Agent** to interpret detected anomalies and raise corresponding maintenance alerts. We'll then implement a **Fault Diagnosis Agent** to determine the root cause of the anomaly to enable preparation for maintenance. The agents will use a number of different tools to accomplish their tasks.

The following drawing illustrates the part of the architecture we will implement in this challenge
[TBD: add image with Anomaly Classification Agent and Fault Diagnosis Agent highlighted]

## Step 1: Create and test initial Anomaly Classification Agent

As a first step we will create an agent to interpret and classify anomalies and raise maintenance alerts if certain thresholds have been violated. The agent will take anomalies for certain machines as input and check against thresholds for that machine type by using json data stored in Cosmos DB.

### Step 1.1. Review initial code for Anomaly Classification Agent 

Examine the Python code in [anomaly_classification_agent.py](./agents/anomaly_classification_agent.py)  
A few things to observe:

- The agent uses two function tools
  - `get_thresholds`: Retrieves specific metric threshold values for certain machine types.
  - `get_machine_data`: Fetches details about machines such as id, model and maintenance history.
- The agent is instructed to output both structured alert data in a specific format and a human readable summary.
- The code will both create the agent and run a sample query aginst it.

### Step 1.2. Run the code

```bash
cd challenge-1/agents
python anomaly_classification_agent.py

```

Verify that the agent responed with a reasonable answer.

### Step 1.3. Test with additional questions

Try the agent with some additional questions by updating the code in [anomaly_classification_agent.py](./agents/anomaly_classification_agent.py)

```python

# Normal condition (no maintenance needed)
result = await agent.run('Hello, can you classify the following metric for machine-002: [{"metric": "drum_vibration", "value": 2.1}]')

# Critical anomaly
result = await agent.run('Hello, can you classify the following metric for machine-005: [{"metric": "mixing_temperature", "value": 175}]')

# Non existing machine
result = await agent.run('Hello, can you classify the following anomalies for machine-007: [{"metric": "curing_temperature", "value": 179.2},{"metric": "cycle_time", "value": 14.5}]')
```

### Step 1.4. Review the agent configuration in Foundry Portal

1. Navigate to [Microsoft Foundry Portal](https://ai.azure.com).

> [!TIP]
> Enable the new portal experience using the toggle in the upper right corner.

1. Select the _build_ tab to list available agents
2. Examine the configuration details for **AnomalyClassificationAgent** you just created.

## Step 2: Use Machine API as MCP tool

Machine information is typically stored in a central system and exposed through an API. Let us adjust the data access to use an existing Machine API instead of accessing a Cosmos DB database directly. In this step you will expose the Machine API as an Model Context Protocol (MCP) server for convenient access from the Agent.

> [!NOTE]
> The Model Context Protocol (MCP) is a standardized way for AI models and systems to communicate context and metadata about their operations. It allows different components of an AI ecosystem to share information seamlessly, enabling better coordination and integration.

### Step 2.1. Test the Machine API

The Machine API is already available in API Management and contains endpoints for listing all machines and get details for a specific machine. Try the API using the following commands

```bash
# Get all machines
curl -fsSL "$APIM_GATEWAY_URL/machines" -H "Ocp-Apim-Subscription-Key: $APIM_SUBSCRIPTION_KEY" -H "Accept: application/json"

# Get a specific machine
curl -fsSL "$APIM_GATEWAY_URL/machines/machine-001" -H "Ocp-Apim-Subscription-Key: $APIM_SUBSCRIPTION_KEY" -H "Accept: application/json"
```

#### Step 2.2. Expose Machine API as an MCP server

API Management provides an easy way to expose APIs as MCP servers without writing any additional wrapper code.

1. Navigate to your API Management instance in the [Azure portal](https://portal.azure.com).
2. Choose **APIs** and notice that **Machine API** you tested earlier is available
3. Navigate to the **MCP Servers** section 
4. 

1. Run Anomaly Detection Agent in notebook. Use threshold + machines as json files. Anomaly as input
2. MCP
    a. Test and Examine Machine API in APIM
    b. Publish Machine API as MCP
    c. Update Anomaly Deteaction Agent to use machine API via MCP

3. Fault Diagnosis Agent with knowledge base
    a. Configure Search/Foundry IQ for additional knowledge data
    b. Test together with machine MCP to get root cause details

4. Add memory
    a. Configure memory for Fault Diagnosis Agent
    b. Test with memory
