# Challenge 2: Building the Repair Planner Agent with GitHub Copilot

Welcome to Challenge 2!

In this challenge we will create an intelligent Repair Planner Agent using .NET that generates comprehensive repair plans and work orders when faults are detected in tire manufacturing equipment. You'll leverage the **@agentplanning** GitHub Copilot agent to guide your development and generate production-ready code.

**Expected Duration:** 30 minutes
**Prerequisites**: [Challenge 0](../challenge-0/challenge-0.md) successfully completed

## 🎯 Objective

- Create a .NET Agent using GitHub Copilot

## 🧭 Context and background information

The **Repair Planner Agent** is the third component in our multi-agent system. After a fault has been diagnosed, this agent determines:

- What repair tasks need to be performed
- Which technician has the required skills
- What parts are needed from inventory
- Creates a structured Work Order in the ERP system

The result from this agent will be used by a fourth agent, the Maintenance Scheduler Agent, that will determine the most optimal maintenance window.

The following drawing illustrates the part of the architecture we will implement in this challenge

[TODO: add image with Repair Planner Agent highlighted]
[TODO: add explanation of GitHub Copilot custom agents]

This repository includes a specialized GitHub Copilot agent called **@agentplanning** that is an expert in building repair planning agents. Instead of manually implementing each component, you'll use this agent to guide your development.

[TODO: verify if @ is used to trigger the agent. It looks to be a drop down selection now]

The **@agentplanning** agent knows:

- Multi-agent system architecture
- .NET and C# best practices
- Microsoft Foundry and Cosmos DB integration
- Predictive maintenance domain knowledge
- Industrial IoT patterns

### Agent-Driven Development Workflow

Follow this workflow when using the agent planner:

1. **Ask the agent to plan** the component architecture
2. **Request code generation** with specific requirements
3. **Review and refine** the generated code
4. **Ask for improvements** or additional features
5. **Request tests** to validate functionality

In our Repair Planner Agent scenario we will use the following workflow and key prompts
> [!NOTE]
> We will use these prompts later in the exercise so you don't need to run them now in Copilot Chat

<details>
<summary>📋 Start with Architecture Planning</summary>

```
@agentplanning I need to build a Repair Planner Agent in .NET for Challenge 2. 
Can you explain the architecture and what components I need to implement?
```

</details>

<details>
<summary>🏗️ Generate Data Models</summary>

```
@agentplanning Create all the data models I need for the Repair Planner Agent:
- DiagnosedFault (input from previous agent)
- Technician (with skills and availability)
- Part (inventory items)
- WorkOrder (output with tasks and resources)
Use proper C# naming conventions and add XML documentation.
```

</details>

<details>
<summary>🗄️ Create Cosmos DB Service</summary>

```
@agentplanning Create a CosmosDbService class that:
- Queries technicians by required skills
- Fetches parts inventory by part numbers
- Creates work orders in Cosmos DB
Include error handling, logging, and async patterns.
```

</details>

<details>
<summary>🤖 Implement AI Integration</summary>

```
@agentplanning Create an AIFoundryService that uses Microsoft Foundry to generate 
repair plans. The service should:
- Accept a diagnosed fault, available technicians, and parts
- Build a structured prompt for the LLM
- Parse the response into a WorkOrder object
- Handle JSON deserialization errors
```

</details>

<details>
<summary> 🔧 Build the Main Agent</summary>

```
@agentplanning Create the main RepairPlanner class that orchestrates:
1. Determining required skills from fault type
2. Querying available technicians
3. Checking parts inventory
4. Generating the repair plan with AI
5. Saving the work order to Cosmos DB
Include comprehensive logging and error handling.
```

</details>
<details>
<summary> 📝 Generate Program.cs </summary>

```
@agentplanning Create a Program.cs that:
- Loads configuration from environment variables
- Initializes all services with dependency injection
- Creates a sample diagnosed fault
- Calls the repair planner
- Displays the work order results
```

</details>

### Best Practices When Using @agentplanning

✅ **DO:**

- Be specific about requirements in your prompts
- Ask the agent to explain design decisions
- Request improvements for generated code
- Use follow-up questions for clarification
- Ask for tests after implementing features

❌ **DON'T:**

- Copy-paste code without understanding it
- Skip reviewing error handling
- Forget to ask about edge cases
- Ignore the agent's explanations

## ✅ Tasks

> [!IMPORTANT]
> The outcome of @agentplanner is heavily dependent on which model is used for GitHub copilot. Bigger models like **GPT-5.2** and **Claud Sonnet 4.5** might be able to solve more complex tasks compared to the smaller models that are included in the GitHub Free version. See [Supported AI models per Copilot plan](https://docs.github.com/en/copilot/reference/ai-models/supported-models#supported-ai-models-per-copilot-plan) for details.

---

### Task 1: Project Setup

```bash
# Navigate to challenge-2 directory
cd /workspaces/factory-ops-hack/challenge-2

# Create a new console application
dotnet new console -n RepairPlannerAgent

# Navigate into project
cd RepairPlannerAgent
```

---

### Task 2: Implement Components with @agentplanning

Now that you understand the workflow, let's build each component. For each section below, use the @agentplanning agent to generate the code.

---

#### Task 2.1: Do architecture planning

Open GitHub Copilot Chat (Ctrl+Shift+I or Cmd+Shift+I) and write (or copy paste) the following promt

```
@agentplanning I need to build a Repair Planner Agent in .NET for Challenge 2. 
Can you explain the architecture and what components I need to implement?
```

---

#### Task 2.1 Create Data Models

**💬 Ask the agent:**

```
@agentplanning Create the Models folder with all necessary data models for the Repair Planner Agent.
```

The agent should generate classes similar to these examples:

<details>
<summary>DiagnosedFault.cs (Input from Challenge 1)</summary>

```csharp
namespace RepairPlannerAgent.Models
{
    public class DiagnosedFault
    {
        public string MachineId { get; set; }
        public string FaultType { get; set; }
        public string RootCause { get; set; }
        public string Severity { get; set; }
        public DateTime DetectedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }
}
```

</details>
<details>
<summary>Technician.cs</summary>

```csharp
namespace RepairPlannerAgent.Models
{
    public class Technician
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Skills { get; set; }
        public bool Available { get; set; }
        public int ExperienceYears { get; set; }
    }
}
```

</details>
<details>
<summary>Part.cs</summary>

```csharp
namespace RepairPlannerAgent.Models
{
    public class Part
    {
        public string PartNumber { get; set; }
        public string Description { get; set; }
        public int QuantityAvailable { get; set; }
        public string Location { get; set; }
    }
}
```

</details>

<details>
<summary>WorkOrder.cs (Output)</summary>

```csharp
namespace RepairPlannerAgent.Models
{
    public class WorkOrder
    {
        public string Id { get; set; }
        public string MachineId { get; set; }
        public string FaultType { get; set; }
        public List<RepairTask> Tasks { get; set; }
        public Technician AssignedTechnician { get; set; }
        public List<Part> RequiredParts { get; set; }
        public DateTime ScheduledStart { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
    }

    public class RepairTask
    {
        public string Description { get; set; }
        public int EstimatedMinutes { get; set; }
        public List<string> RequiredTools { get; set; }
    }
}
```

</details>

#### Task 2.2 Implement Cosmos DB Service

**💬 Ask the agent:**

```
@agentplanning Implement the CosmosDbService with methods to:
1. Query technicians with specific skills who are available
2. Get parts by part numbers
3. Create new work orders
Use proper error handling and async/await patterns.
```

The agent will generate code similar to this structure:

<details>
<summary>CosmosDbService.cs</summary>

```csharp
using Microsoft.Azure.Cosmos;
using RepairPlannerAgent.Models;

namespace RepairPlannerAgent.Services
{
    public class CosmosDbService
    {
        private readonly CosmosClient _client;
        private readonly Container _techniciansContainer;
        private readonly Container _partsContainer;
        private readonly Container _machinesContainer;
        private readonly Container _workOrdersContainer;

        public CosmosDbService(string endpoint, string key, string databaseName)
        {
            _client = new CosmosClient(endpoint, key);
            var database = _client.GetDatabase(databaseName);
            
            _techniciansContainer = database.GetContainer("Technicians");
            _partsContainer = database.GetContainer("PartsInventory");
            _machinesContainer = database.GetContainer("Machines");
            _workOrdersContainer = database.GetContainer("WorkOrders");
        }

        public async Task<List<Technician>> GetAvailableTechniciansWithSkillsAsync(List<string> requiredSkills)
        {
            // Agent will implement the query logic
        }

        public async Task<List<Part>> GetPartsInventoryAsync(List<string> partNumbers)
        {
            // Agent will implement the query logic
        }

        public async Task<string> CreateWorkOrderAsync(WorkOrder workOrder)
        {
            // Agent will implement the insertion logic
        }
    }
}
```

</details>

---

#### Task 2.3 Implement AI Foundry Service

**💬 Ask the agent:**

```
@agentplanning Create an AIFoundryService that generates repair plans using Microsoft Foundry. 
The service should send a detailed prompt with fault information, available technicians, 
and parts, then parse the response into a WorkOrder object.
```

The agent will generate code similar to this:

<details>
<summary>AIFoundryService.cs</summary>

```csharp
using Azure.AI.Inference;
using Azure.Core;
using System.Text.Json;
using RepairPlannerAgent.Models;

namespace RepairPlannerAgent.Services
{
    public class AIFoundryService
    {
        private readonly ChatCompletionsClient _client;
        private readonly string _modelDeployment;

        public AIFoundryService(string endpoint, string key, string modelDeployment)
        {
            _client = new ChatCompletionsClient(
                new Uri(endpoint),
                new AzureKeyCredential(key)
            );
            _modelDeployment = modelDeployment;
        }

        public async Task<WorkOrder> GenerateRepairPlanAsync(
            DiagnosedFault fault,
            List<Technician> availableTechnicians,
            List<Part> availableParts)
        {
            // Build system prompt
            var systemPrompt = @"You are an expert maintenance planner for tire manufacturing equipment.
Generate a detailed repair plan with specific tasks, timeline, and resource allocation.
Return the response as valid JSON matching the WorkOrder schema.";

            // Build user prompt with context
            var userPrompt = $@"
Generate a repair plan for:
- Machine: {fault.MachineId}
- Fault Type: {fault.FaultType}
- Root Cause: {fault.RootCause}
- Severity: {fault.Severity}

Available Technicians:
{JsonSerializer.Serialize(availableTechnicians)}

Available Parts:
{JsonSerializer.Serialize(availableParts)}

Requirements:
1. Break down repair into specific tasks
2. Assign the most qualified technician
3. List required parts and tools
4. Estimate duration for each task
5. Set priority based on severity
";

            // Agent will implement the AI Foundry call
        }
    }
}
```

</details>

---

#### Task 2.4 Create the Main Agent

**💬 Ask the agent:**

```
@agentplanning Create the main RepairPlanner class that orchestrates the entire workflow.
It should determine required skills, query technicians and parts, call the AI service, 
and save the work order.
```

The agent will generate code similar to this:

<details>
<summary>RepairPlanner.cs</summary>

```csharp
using Microsoft.Extensions.Logging;
using RepairPlannerAgent.Models;
using RepairPlannerAgent.Services;

namespace RepairPlannerAgent
{
    public class RepairPlanner
    {
        private readonly CosmosDbService _cosmosService;
        private readonly AIFoundryService _aiService;
        private readonly ILogger<RepairPlanner> _logger;

        public RepairPlanner(
            CosmosDbService cosmosService,
            AIFoundryService aiService,
            ILogger<RepairPlanner> logger)
        {
            _cosmosService = cosmosService;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<WorkOrder> PlanRepairAsync(DiagnosedFault fault)
        {
            _logger.LogInformation($"Planning repair for machine {fault.MachineId}");

            try
            {
                // Task 1: Determine required skills based on fault type
                var requiredSkills = DetermineRequiredSkills(fault.FaultType);
                _logger.LogInformation($"Required skills: {string.Join(", ", requiredSkills)}");

                // Task 2: Query available technicians
                var technicians = await _cosmosService.GetAvailableTechniciansWithSkillsAsync(requiredSkills);
                
                if (!technicians.Any())
                {
                    throw new Exception("No technicians available with required skills");
                }

                // Task 3: Determine required parts
                var requiredPartNumbers = DetermineRequiredParts(fault.FaultType);
                var parts = await _cosmosService.GetPartsInventoryAsync(requiredPartNumbers);

                // Task 4: Check parts availability
                var missingParts = parts.Where(p => p.QuantityAvailable == 0).ToList();
                if (missingParts.Any())
                {
                    _logger.LogWarning($"Missing parts: {string.Join(", ", missingParts.Select(p => p.PartNumber))}");
                    // TODO: Trigger SCM order in future challenge
                }

                // Task 5: Use AI to generate detailed repair plan
                var workOrder = await _aiService.GenerateRepairPlanAsync(fault, technicians, parts);

                // Task 6: Save work order to Cosmos DB
                workOrder.Id = Guid.NewGuid().ToString();
                workOrder.Status = "Scheduled";
                await _cosmosService.CreateWorkOrderAsync(workOrder);

                _logger.LogInformation($"Work order {workOrder.Id} created successfully");
                return workOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error planning repair");
                throw;
            }
        }

        private List<string> DetermineRequiredSkills(string faultType)
        {
            // Map fault types to required skills
            // Ask @agentplanning to expand this with more fault types
            return faultType.ToLower() switch
            {
                // Representative examples from challenge-0/data/fault-skills-mapping.json
                "curing_temperature_excessive" => new List<string>
                {
                    "tire_curing_press",
                    "temperature_control",
                    "instrumentation",
                    "electrical_systems",
                    "plc_troubleshooting",
                    "mold_maintenance"
                },
                "building_drum_vibration" => new List<string>
                {
                    "tire_building_machine",
                    "vibration_analysis",
                    "bearing_replacement",
                    "alignment",
                    "precision_alignment",
                    "drum_balancing",
                    "mechanical_systems"
                },
                _ => new List<string> { "General Maintenance" }
            };
        }

        private List<string> DetermineRequiredParts(string faultType)
        {
            // Map fault types to part numbers
            // Ask @agentplanning to expand this with more fault types
            return faultType.ToLower() switch
            {
                // Representative examples from challenge-0/data/fault-parts-mapping.json
                "curing_temperature_excessive" => new List<string> { "TCP-HTR-4KW", "GEN-TS-K400" },
                "ply_tension_excessive" => new List<string> { "TBM-LS-500N", "TBM-SRV-5KW" },
                _ => new List<string>()
            };
        }
    }
}
```

</details>

---

#### Task 2.5 Create Program.cs

**💬 Ask the agent:**

```
@agentplanning Create Program.cs that initializes all services, creates a sample fault, 
and demonstrates the repair planning workflow.
```

The agent should generate code like this:

<details>
<summary>Program.cs</summary>

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepairPlannerAgent;
using RepairPlannerAgent.Models;
using RepairPlannerAgent.Services;

// Load configuration
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

var logger = loggerFactory.CreateLogger<Program>();

try
{
    // Initialize services
    var cosmosService = new CosmosDbService(
        configuration["COSMOS_ENDPOINT"],
        configuration["COSMOS_KEY"],
        configuration["COSMOS_DATABASE_NAME"]
    );

    var aiService = new AIFoundryService(
        configuration["AZURE_AI_CHAT_ENDPOINT"],
        configuration["AZURE_AI_CHAT_KEY"],
        configuration["AZURE_AI_CHAT_MODEL_DEPLOYMENT_NAME"]
    );

    var repairPlanner = new RepairPlanner(
        cosmosService,
        aiService,
        loggerFactory.CreateLogger<RepairPlanner>()
    );

    // Example: Process a diagnosed fault
    var sampleFault = new DiagnosedFault
    {
        MachineId = "machine-001",
        FaultType = "curing_temperature_excessive",
        RootCause = "Temperature control issue causing curing temperature to exceed threshold",
        Severity = "High",
        DetectedAt = DateTime.UtcNow,
        Metadata = new Dictionary<string, object>
        {
            { "Temperature", 185 },
            { "Threshold", 150 },
            { "Location", "Curing Bay 3" }
        }
    };

    logger.LogInformation("Starting repair planning process...");
    var workOrder = await repairPlanner.PlanRepairAsync(sampleFault);

    logger.LogInformation("=== WORK ORDER CREATED ===");
    logger.LogInformation($"Work Order ID: {workOrder.Id}");
    logger.LogInformation($"Assigned Technician: {workOrder.AssignedTechnician.Name}");
    logger.LogInformation($"Estimated Duration: {workOrder.EstimatedDurationMinutes} minutes");
    logger.LogInformation($"Priority: {workOrder.Priority}");
    logger.LogInformation($"Status: {workOrder.Status}");
    
    Console.WriteLine("\n✅ Repair plan generated successfully!");
}
catch (Exception ex)
{
    logger.LogError(ex, "Application failed");
    Environment.Exit(1);
}
```

</details>

---

### Task 3: Testing Your Agent

#### Task 3.1 Build the Project

```bash
dotnet build
```

---

#### Task 3.2 Run the Agent

```bash
# Load environment variables
export $(cat ../.env | xargs)

# Run the application
dotnet run
```

---

#### Task 3.3 Expected Output

```
info: RepairPlannerAgent.RepairPlanner[0]
      Planning repair for machine MACHINE-CURE-001
info: RepairPlannerAgent.RepairPlanner[0]
      Required skills: HVAC Systems, Electrical
info: RepairPlannerAgent.RepairPlanner[0]
      Work order e7f3a89c-4d5f-4a1b-9b2e-8c7d6e5f4a3b created successfully
info: Program[0]
      === WORK ORDER CREATED ===
info: Program[0]
      Work Order ID: e7f3a89c-4d5f-4a1b-9b2e-8c7d6e5f4a3b
info: Program[0]
      Assigned Technician: John Smith
info: Program[0]
      Estimated Duration: 120 minutes
info: Program[0]
      Priority: High
info: Program[0]
      Status: Scheduled

✅ Repair plan generated successfully!
```

---

### Task 4 (optional): Enhance with @agentplanning

Once your basic agent is working, use @agentplanning to add advanced features

---

#### Task 4.1 Add Advanced Features


<details>

<summary>Priority Calculation</summary>

```
@agentplanning Add a PriorityCalculator class that determines work order priority based on:
- Fault severity (Critical/High/Medium/Low)
- Machine criticality (from Cosmos DB)
- Production impact (estimated downtime cost)
- Parts availability (delay if parts need ordering)
Return a priority score and category.
```
</details>

<details>
<summary>Smart Scheduling</summary>

```
@agentplanning Add a method to find the optimal maintenance window:
- Query production schedule from MES container
- Avoid peak production hours
- Check technician availability
- Calculate minimum production impact
Return the best time slot with reasoning.
```
</details>

<details>
<summary>Smart Scheduling</summary>

```
@agentplanning Create a SupplyChainService that:
- Checks if parts quantity is below reorder threshold
- Generates purchase orders in SCM container
- Estimates delivery time
- Updates work order status if parts are on order
```

</details>

---

#### Task 4.2 Improve Error Handling
<details>
<summary>Comprehensive Error Handling</summary>
```
@agentplanning Review my code and add comprehensive error handling:
- Retry logic for transient Cosmos DB failures
- Fallback to generalist technician if no specialists available
- Validation for AI-generated JSON responses
- Graceful degradation if AI service is unavailable
```
</details>

---

### Task 5 (optional): Testing with @agentplanning

#### Task 5.1 Generate Unit Tests

Create a test project:

```bash
dotnet new xunit -n RepairPlannerAgent.Tests
cd RepairPlannerAgent.Tests
dotnet add reference ../RepairPlannerAgent/RepairPlannerAgent.csproj
dotnet add package Moq
```

**💬 Ask the agent:**

```
@agentplanning Create comprehensive unit tests for the RepairPlanner class:
- Mock CosmosDbService and AIFoundryService
- Test DetermineRequiredSkills with various fault types
- Test DetermineRequiredParts logic
- Test error handling when no technicians available
- Test error handling when parts are missing
- Verify work order is created correctly
Use xUnit and Moq for mocking.
```

#### Task 5.2 Integration Tests

Test against actual Azure resources:

- Query real technician data from Cosmos DB
- Generate repair plans using live AI Foundry
- Verify work orders are created correctly

### Success criteria

After completing the tasks, you should have:

- [ ] A .NET project created with all required packages
- [ ] Used @agentplanning to generate all data models
- [ ] Used @agentplanning to create Cosmos DB service
- [ ] Used @agentplanning to implement AI Foundry integration
- [ ] Work orders are created and stored in Cosmos DB
- [ ] Agent handles errors gracefully with logging
- [ ] Successfully generated at least 3 work orders for different fault types
- [ ] Used @agentplanning for at least 80% of code development
- [ ] Asked follow-up questions to improve generated code
- [ ] Added unit tests with @agentplanning assistance

## 🛠️ Troubleshooting and FAQ

- [TODO: add info about different models]

## 🧠 Conclusion and reflection

🎉 Congratulations! By completing this challenge, you have built a sophisticated Repair Planner Agent using AI-driven development with the @agentplanning agent. You've learned how to effectively collaborate with specialized GitHub Copilot agents to accelerate your development workflow while maintaining production-quality code. In the next challenges, you will expand this multi-agent system by adding maintenance scheduling capabilities and orchestrating all agents to work together seamlessly in a complete predictive maintenance solution.

[TODO: add section about vibe coding vs structured guiding. Use Spec Kit as example as well]

If you want to expand your knowledge on what we-ve covered in this challenge, have a look at the content below:
[TODO: review links]

- [Microsoft Foundry Documentation](https://learn.microsoft.com/azure/ai-studio/)
- [Azure Cosmos DB .NET SDK](https://learn.microsoft.com/azure/cosmos-db/nosql/sdk-dotnet-v3)
- [GitHub Copilot Documentation](https://docs.github.com/copilot)
- [.NET 10 Documentation](https://learn.microsoft.com/en-us/dotnet/)
- [Microsoft Agent Framework](https://github.com/microsoft/semantic-kernel)

**Next step:** [Challenge 3](../challenge-3/challenge-3.md) - Maintenance Scheduler & Parts Ordering Agents
