# Challenge 2: Building the Repair Planner Agent with GitHub Copilot

Welcome to Challenge 2!

In this challenge you will create an intelligent Repair Planner Agent using .NET that generates comprehensive repair plans and work orders when faults are detected in tire manufacturing equipment. You'll leverage the **@agentplanning** GitHub Copilot agent to guide your development and generate production-ready code.

**Expected Duration:** 30 minutes  
**Prerequisites**: [Challenge 0](../challenge-0/README.md) successfully completed

## 🎯 Objective

Create a .NET Agent using the **Foundry Agents SDK** pattern with GitHub Copilot assistance.

## 🧭 Context and Background

The **Repair Planner Agent** is the third component in our multi-agent system. After a fault has been diagnosed, this agent:

- Determines what repair tasks need to be performed
- Finds technicians with the required skills
- Checks parts inventory
- Creates a structured Work Order in Cosmos DB

### Architecture Overview

This agent uses the **Foundry Agents SDK** pattern (same approach as the Python challenges):

```
┌─────────────────────────────────────────────────────────────┐
│                    RepairPlannerAgent                        │
├─────────────────────────────────────────────────────────────┤
│  1. FaultMappingService    → Get required skills & parts    │
│  2. CosmosDbService        → Query technicians & inventory  │
│  3. AIProjectClient        → Create/invoke Foundry Agent    │
│  4. CosmosDbService        → Save work order                │
└─────────────────────────────────────────────────────────────┘
```

### Using @agentplanning

This repository includes a specialized GitHub Copilot agent called **@agentplanning** that knows:

- Foundry Agents SDK patterns (`Azure.AI.Projects` + `Microsoft.Agents.AI`)
- .NET and C# best practices
- Cosmos DB integration
- The fault→skills/parts mappings for this workshop

## ✅ Tasks

> [!IMPORTANT]
> The outcome depends on which model GitHub Copilot uses. Larger models (GPT-5.2, Claude Sonnet 4.5) may handle more complex prompts. Smaller models work better with focused, single-file requests.

---

### Task 1: Project Setup

```bash
# Navigate to challenge-2 directory
cd challenge-2

# Create a new console application
dotnet new console -n RepairPlanner

# Navigate into project
cd RepairPlanner

```

---

### Task 2: Create Components with @agentplanning

Open GitHub Copilot Chat (Ctrl+Shift+I or Cmd+Shift+I) and use the @agentplanning agent.

#### Task 2.1: Architecture Planning

```
@agentplanning I need to build a Repair Planner Agent in .NET for Challenge 2 
using the Foundry Agents SDK. Can you explain the architecture?
```

#### Task 2.2: Create Data Models

```
@agentplanning Create all data models for the Repair Planner Agent:
- DiagnosedFault (input from previous agent)
- Technician (with skills and availability) 
- Part (inventory items)
- WorkOrder (output with tasks)
- RepairTask (individual repair steps)
- WorkOrderPartUsage (parts needed)

Use dual JSON attributes for Cosmos DB compatibility.
```

<details>
<summary>📋 Expected Model Structure</summary>

Each model should have both `[JsonPropertyName]` and `[JsonProperty]` attributes:

```csharp
using System.Text.Json.Serialization;
using Newtonsoft.Json;

public sealed class WorkOrder
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    // ... more properties
}
```

</details>

#### Task 2.3: Create FaultMappingService

```
@agentplanning Create a FaultMappingService with IFaultMappingService interface 
that maps fault types to required skills and parts using hardcoded dictionaries.
```

#### Task 2.4: Create CosmosDbService

```
@agentplanning Create a CosmosDbService that:
- Queries available technicians by skills
- Fetches parts by part numbers  
- Creates work orders
Include error handling and logging.
```

#### Task 2.5: Create the Main Agent

```
@agentplanning Create RepairPlannerAgent.cs using the Foundry Agents SDK pattern:
- Use AIProjectClient and PromptAgentDefinition
- Use primary constructor
- Include EnsureAgentVersionAsync to register the agent
- Include PlanAndCreateWorkOrderAsync to generate work orders
- Handle JSON parsing with NumberHandling.AllowReadingFromString
```

<details>
<summary>📋 Expected Agent Pattern</summary>

```csharp
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Agents.AI;

public sealed class RepairPlannerAgent(
    AIProjectClient projectClient,
    CosmosDbService cosmosDb,
    IFaultMappingService faultMapping,
    string modelDeploymentName,
    ILogger<RepairPlannerAgent> logger)
{
    private const string AgentName = "RepairPlannerAgent";
    
    public async Task EnsureAgentVersionAsync(CancellationToken ct = default)
    {
        var definition = new PromptAgentDefinition(model: modelDeploymentName)
        {
            Instructions = "..."
        };
        await projectClient.Agents.CreateAgentVersionAsync(
            AgentName, 
            new AgentVersionCreationOptions(definition), 
            ct);
    }
    
    public async Task<WorkOrder> PlanAndCreateWorkOrderAsync(DiagnosedFault fault, CancellationToken ct = default)
    {
        // 1. Get skills/parts from mapping
        // 2. Query Cosmos DB
        // 3. Build prompt and invoke agent
        // 4. Parse and save work order
    }
}
```

</details>

#### Task 2.6: Create Program.cs

```
@agentplanning Create Program.cs that:
- Sets up dependency injection
- Loads config from environment variables (AZURE_AI_PROJECT_ENDPOINT, MODEL_DEPLOYMENT_NAME, COSMOS_*)
- Creates a sample DiagnosedFault
- Calls the repair planner
- Displays the work order

Add comments explaining C# idioms for Python developers.
```

---

### Task 3: Test Your Agent

#### Set Environment Variables

```bash
export AZURE_AI_PROJECT_ENDPOINT="https://your-project.api.azureml.ms"
export MODEL_DEPLOYMENT_NAME="gpt-4o"
export COSMOS_ENDPOINT="https://your-cosmos.documents.azure.com:443/"
export COSMOS_KEY="your-key"
export COSMOS_DATABASE_NAME="FactoryDB"
```

#### Build and Run

```bash
dotnet build
dotnet run
```

#### Expected Output

```
12:34:56 info: RepairPlannerAgent[0] Creating agent 'RepairPlannerAgent' with model 'gpt-4o'
12:34:57 info: RepairPlannerAgent[0] Agent version: abc123
12:34:57 info: RepairPlannerAgent[0] Planning repair for machine-001, fault=curing_temperature_excessive
12:34:58 info: CosmosDbService[0] Found 3 available technicians matching skills
12:34:58 info: CosmosDbService[0] Fetched 2 parts
12:34:58 info: RepairPlannerAgent[0] Invoking agent 'RepairPlannerAgent'
12:35:05 info: Program[0] Saved work order WO-2026-001 (id=xxx, status=new, assignedTo=tech-001)

{
  "id": "...",
  "workOrderNumber": "WO-2026-001",
  "machineId": "machine-001",
  "title": "Repair Curing Temperature Issue",
  ...
}
```

---

### Task 4 (Optional): Enhancements

Once the basic agent works, try adding:

```
@agentplanning Add priority calculation based on fault severity
```

```
@agentplanning Add better error handling for when no technicians are available
```

```
@agentplanning Add structured output using AIJsonUtilities.CreateJsonSchema 
and ChatResponseFormat.ForJsonSchema for type-safe responses
```

---

## 🏗️ Project Structure

Your completed project should look like:

```
RepairPlanner/
├── RepairPlanner.csproj
├── Program.cs
├── RepairPlannerAgent.cs
├── Models/
│   ├── DiagnosedFault.cs
│   ├── Technician.cs
│   ├── Part.cs
│   ├── WorkOrder.cs
│   ├── RepairTask.cs
│   └── WorkOrderPartUsage.cs
└── Services/
    ├── CosmosDbService.cs
    ├── CosmosDbOptions.cs
    └── FaultMappingService.cs
```

---

## ✅ Success Criteria

After completing the tasks, you should have:

- [ ] A .NET project with all required packages
- [ ] Data models with dual JSON attributes
- [ ] FaultMappingService with hardcoded mappings
- [ ] CosmosDbService for data access
- [ ] RepairPlannerAgent using Foundry Agents SDK
- [ ] Work orders created and stored in Cosmos DB
- [ ] Used @agentplanning for at least 80% of code generation

---

## 🛠️ Troubleshooting

### "Preview API" warnings

Add `<NoWarn>$(NoWarn);CA2252</NoWarn>` to your `.csproj`.

### JSON parsing errors with numbers

LLMs sometimes return `"60"` instead of `60`. Use:

```csharp
NumberHandling = JsonNumberHandling.AllowReadingFromString
```

### Cosmos DB errors

Ensure you're using both `[JsonPropertyName]` and `[JsonProperty]` attributes on models.

### Agent not invoking correctly

Make sure you call `EnsureAgentVersionAsync()` before `PlanAndCreateWorkOrderAsync()`.

---

## 🧠 Conclusion

🎉 Congratulations! You've built a Repair Planner Agent using the Foundry Agents SDK pattern - the same approach used in the Python challenges. This pattern:

- Creates a named agent with instructions
- Registers it with Azure AI Foundry
- Invokes it with user prompts
- Returns structured responses

**Next step:** [Challenge 3](../challenge-3/README.md) - Maintenance Scheduler & Parts Ordering Agents
