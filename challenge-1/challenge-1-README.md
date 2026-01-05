# Challenge 1: Agent Framework Agents for Anomaly Classification and Fault Diagnosis

**Expected Duration**: 60 min

Welcome to Challenge 1!

In this challenge, we will build two specialized agents for classifying and understanding machine anomalies. First we'll develop an **Anomaly Classification Agent** to interpret detected anomalies and raise corresponding maintenance alerts. We'll then implement a **Fault Diagnosis Agent** to determine the root cause of the anomaly to enable preparation for maintenance. The agents will use a number of different tools to accomplish their tasks.

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

## Step 2 : Use Machine API as MCP tool

Machine information is typically stored in a central system and exposed through an API. Let's adjust the data access to use an existing Machine API instead of accessing a Cosmos DB database directly. In this step you will expose the Machine API as an Model Context Protocol (MCP) server for convenient access from the Agent.

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

### Step 2.2. Expose Machine API as an MCP server

API Management provides an easy way to expose APIs as MCP servers without writing any additional wrapper code.

1. Navigate to your API Management instance in the [Azure portal](https://portal.azure.com).
2. Choose _APIs_ and notice that _Machine API_ you tested earlier is available
3. Navigate to the _MCP Servers_ section
4. Click _Create MCP Server_ and _Expose an API as MCP Server_
5. Select API, operations and provide the following details
    - **API**: _Machine API_
    - **API Operations**: _Get Machine_
    - **Display Name**: _Get Machine Data_
    - **Name**: _get-machine-data_
    - **Description**: _Gets details about a specific machine_
6. Click _Create_
7. Finally, save the _MCP Server URL_ of the newly created MCP server, you will need it in the next part. Add a new entry with the value in the '.env' file:

```bash
MACHINE_MCP_SERVER_ENDPOINT=<MCP_SERVER_URL>
```

### Step 2.3. Use the Machine MCP Server

Now its time to replace the direct database access with our new Machine MCP Server. The MCP server will be added as as tool to the Anomaly Classification Agent.

1. Add the following import at the top of [anomaly_classification_agent.py](./agents/anomaly_classification_agent.py)

    ```python
    from agent_framework import HostedMCPTool
    ```

2. Locate the `# TODO: add subscription key and MCP endpoint` and add the following variables

    ```python
    mcp_endpoint = os.environ.get("MACHINE_MCP_SERVER_ENDPOINT")
    mcp_subscription_key = os.environ.get("APIM_SUBSCRIPTION_KEY")
    ```

3. locate the tools assignment

    ```python
    tools=[
        get_machine_data,
        get_thresholds]
    ```

    and replace it with the following code

    ```python

    tools=[HostedMCPTool(name="Machine Data", url=mcp_endpoint, approval_mode="never_require", 
                        headers={"Ocp-Apim-Subscription-Key": os.environ.get("APIM_SUBSCRIPTION_KEY")}), 
                        get_thresholds]
    ```

### Step 2.4. Test the agent with MCP tool

Run the code

```bash
cd challenge-1/agents
python anomaly_classification_agent.py

```

Verify that the agent responed with a correct answer.

## Step 3: Understand root cause with Fault Diagnosis Agent and Foundry IQ

The next agent we'll create, **Fault Diagnosis Agent**, is tasked to understand the actual root cause of the issues alerted from the **Anomaly Classification Agent**. Besides machine data and maintenance history we'll add a machine wiki as a tool for the agent by leveraging **Foundry IQ**.

> [!NOTE]
> Foundry IQ is an agentic retrieval workload powered by Azure AI Search that defines a reusable knowledge base around a topic.
> An agent connects to Foundry IQ using Model Context Protocol (MCP) to facilitate tool calls.

### Step 3.1. Examine the machine data wiki

The machine wiki contains knowledge (common issues, repair instructions and repair details) about different machine types. The wiki pages are available as markdown files in **Azure Blob Storage**. Take a moment to review the content:

1. Navigate to [Azure Portal](https://portal.azure.com) and locate the storage account.
2. Select _Storage browser_ / _Blob containers_ and select the _machine-wiki_ container  
3. Select a wiki article and selecte the _edit_ tab to preview the content

### Step 3.2. Expose the machine wiki data as a knowledge base

**Foundry IQ** consists of knowledge sources (_what_ to retrieve) and knowledge bases (_how_ to retrieve). Knowledge sources are created as standalone objects and then referenced in a knowledge base.

> [!NOTE]
> [Foundry Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/overview?view=foundry) orchestrates calls to the knowledge base via the MCP tool and synthesizes the final answer. At runtime, the agent calls only the knowledge base, not the data platform (such as **Azure Blob Storage** in our case) that underlies the knowledge source. The knowledge base handles all retrieval operations.

Create a knowledge source, knowledge base and project connection using the [create_knowledge_base.ipynb](./create_knowledge_base.ipynb) notebook.

### Step 3.4. Create the Fault Diagnosis Agent

Let's create the **Fault Diagnosis Agent** and use our newly created Foundry IQ knowledge base.

Examine the Python code in [fault_diagnosis_agent.py](./agents/fault_diagnosis_agent.py)  

Currently only one tool `machine_data` is available. Your task is to add the knowledge base MCP tool to the agent so the machine wiki content can be used when diagnosing the root cause of the anomaly.

1. Locate placeholder comment `# TODO: add Foundry IQ MCP tool`  in [fault_diagnosis_agent.py](./agents/fault_diagnosis_agent.py)
2. Add the knowledge base as a `HostedMCPTool` by updating the placeholder with the following code

    ```python
    
    HostedMCPTool(name="Knowledge Base", url=machine_wiki_mcp_endpoint, approval_mode="never_require", allowed_tools=["knowledge_base_retrieve"],
                  headers={"api-key": search_key})
    ```

    > [!TIP]
    > The tools property is a list so make sure you add a trailing comma after the first tool

A few things to observe:

- The agent now uses two MCP tools
  - `knowledge_base`: Retrieves machine wiki information for root cause analysis.
  - `machine_data`: Fetches details about machines such as id, model and maintenance history.
- The agent is clearly instructed to use our machine knowledge base instead of its own knowledge.

Run the code

```bash
cd challenge-1/agents
python fault_diagnosis_agent.py 

```

Verify the answer from the agent

## Conclusion 🎉

Congratulations! You've successfully built two agents and equipped them with enterprise tools to perform their tasks.

**Next Steps: Building the Repair Planner Agent with GitHub Copilot
