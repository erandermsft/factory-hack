# Challenge 3: Predictive Maintenance & Parts Ordering Agents

**Expected Duration:** 60 minutes

## Introduction

In this challenge, you'll build two specialized AI agents that work together to optimize factory operations through predictive maintenance and automated supply chain management:

- **Predictive Maintenance Agent**: Analyzes machine telemetry and historical failure data to predict equipment failures before they occur. It calculates risk scores, determines optimal maintenance windows, and schedules preventive maintenance to minimize production downtime.

- **Parts Ordering Agent**: Monitors inventory levels, evaluates supplier performance, and automates parts ordering to ensure required components are available when needed. It optimizes order timing, supplier selection, and delivery schedules to support maintenance operations.

Both agents leverage Microsoft Foundry's Persistent Agents with thread-based conversation memory, enabling them to maintain context across multiple sessions. This allows the Predictive Maintenance Agent to track machine-specific patterns and degradation trends over time, while the Parts Ordering Agent can reference historical supplier performance and inventory patterns to make more informed decisions.

### Why Agent Memory Matters

Microsoft Foundry's conversational memory allows agents to maintain context across multiple interactions. The Predictive Maintenance Agent uses memory to track machine degradation trends and identify failure patterns over time. The Parts Ordering Agent leverages memory to evaluate supplier reliability and optimize inventory management based on historical data. This enables both agents to deliver increasingly accurate, data-driven recommendations through continuous learning from operational patterns.

### Building Agents with .NET and Microsoft Foundry v2

You'll use the Azure AI Projects SDK to create agents programmatically by connecting to your Microsoft Foundry project with the `AIProjectClient`. Each agent is configured with a GPT-4 model, domain-specific system instructions, and integration with Cosmos DB containers (ERP, MES, WMS, SCM). The .NET SDK's strongly-typed classes and async/await patterns enable seamless interaction with factory data for calculating risk scores, checking inventory, and generating optimized orders. Agents run by creating conversation threads that query Cosmos DB, analyze data, and persist AI-powered decisions back to the database.

## 1. Create Predictive Maintenance Agent

1. Edit `PredMaintenanceAgent/CreateAgent.cs`:
   - Uncomment line 12: `static async Task Main(string[] args)`
   - Comment out line 13: `// static async Task MainCreate(string[] args)`

2. Edit `PredMaintenanceAgent/Program.cs`:
   - Comment out line 9: `// static async Task Main(string[] args)`

3. Run the creation script:
```bash
cd /workspaces/factory-ops-hack/challenge-3/PredMaintenanceAgent
set -a && source ../../.env && set +a
dotnet run
```

## 2. Create Parts Ordering Agent

1. Edit `PartsOrderingAgent/CreateAgent.cs`:
   - Uncomment `static async Task Main(string[] args)`
   - Comment out the alternative Main method name

2. Edit `PartsOrderingAgent/Program.cs`:
   - Comment out `static async Task Main(string[] args)`

3. Run the creation script:
```bash
cd /workspaces/factory-ops-hack/challenge-3/PartsOrderingAgent
set -a && source ../../.env && set +a
dotnet run
```

Copy the Agent IDs from the output and they will be automatically added to your `.env` file.

### 3. Configure Environment Variables

Your `.env` file should contain (automatically added during creation):

```bash
PRED_MAINTENANCE_AGENT_ID=<predictive-maintenance-agent-id>
PARTS_ORDERING_AGENT_ID=<parts-ordering-agent-id>
COSMOS_DATABASE_NAME=FactoryOpsDB
```

**Note:** The current implementation expects specific Cosmos DB containers (WorkOrders, Machines, Telemetry, etc.) created by Challenge 0. If you encounter "container not found" errors, verify that Challenge 0 data seeding completed successfully.

## 4. Running the Predictive Maintenance Agent

**Before running, ensure you've reverted the Main method edits:**
1. Edit `PredMaintenanceAgent/Program.cs`: Uncomment line 9 `static async Task Main(string[] args)`
2. Edit `PredMaintenanceAgent/CreateAgent.cs`: Comment out line 12 `// static async Task Main(string[] args)`

```bash
cd /workspaces/factory-ops-hack/challenge-3/PredMaintenanceAgent
set -a && source ../../.env && set +a
dotnet run wo-2024-445
```

**Expected Output:**
1. ✓ Retrieves work order from Cosmos DB (ERP container)
2. ✓ Analyzes historical maintenance data (CMSS container)
3. ✓ Checks available maintenance windows (MES container)
4. ✓ Runs AI predictive analysis using the agent
5. ✓ Saves maintenance schedule to Cosmos DB
6. ✓ Updates work order status to 'Scheduled'

**What it does:**
- Calculates risk scores based on historical failure patterns
- Computes MTBF (Mean Time Between Failures)
- Predicts failure probability
- Recommends optimal maintenance windows
- Considers production impact and urgency

## 4. Running the Parts Ordering Agent

**Before running, ensure you've reverted the Main method edits:**
1. Edit `PartsOrderingAgent/Program.cs`: Uncomment `static async Task Main(string[] args)`
2. Edit `PartsOrderingAgent/CreateAgent.cs`: Comment out `static async Task Main(string[] args)`

```bash
cd /workspaces/factory-ops-hack/challenge-3/PartsOrderingAgent
set -a && source ../../.env && set +a
dotnet run wo-2024-456
```

**Expected Output:**
1. ✓ Retrieves work order from Cosmos DB
2. ✓ Checks inventory status for required parts (WMS container)
3. ✓ Identifies parts needing ordering
4. ✓ Finds available suppliers (SCM container)
5. ✓ Generates optimized parts order using AI
6. ✓ Saves order to SCM system
7. ✓ Updates work order status to 'PartsOrdered'

**What it does:**
- Analyzes inventory levels against reorder points
- Selects optimal suppliers based on reliability and lead time
- Calculates expected delivery dates
- Optimizes order costs
- Determines order urgency

## 5. Testing End-to-End Workflow

Run both agents in sequence to complete the full maintenance workflow:

```bash
cd /workspaces/factory-ops-hack/challenge-3

# Load environment variables
set -a && source ../.env && set +a

# Step 1: Run Predictive Maintenance Agent
cd PredMaintenanceAgent
dotnet run wo-2024-432

# Step 2: Run Parts Ordering Agent
cd ../PartsOrderingAgent
dotnet run wo-2024-432
```

Both agents are created using Microsoft Foundry's **Persistent Agents** which include:

- **Thread-based Memory**: Each conversation thread maintains its own context and message history
- **Cross-session Persistence**: Agents can reference previous analyses and decisions
- **Machine-specific Context**: Can maintain separate threads for different machines to track patterns
- **Portal Visibility**: Agents are visible and manageable in the Microsoft Foundry portal

## 6. Validation

**Verify containers were created:**

The agents automatically create the following containers in Cosmos DB when they run:
- **MaintenanceSchedules** - Created by Predictive Maintenance Agent
- **PartsOrders** - Created by Parts Ordering Agent (if parts were needed)
- **Suppliers** - Created by Parts Ordering Agent (if supplier lookup performed)
- **MaintenanceHistory** - Created by Predictive Maintenance Agent (for historical data)
- **MaintenanceWindows** - Created by Predictive Maintenance Agent (for scheduling)

**Check results in Azure Cosmos DB:**

1. Navigate to your Cosmos DB account in Azure Portal
2. Go to **Data Explorer**
3. Click **Refresh** to see newly created containers
4. Expand the `FactoryOpsDB` database
5. Check the containers:
   - **MaintenanceSchedules** container: Should have maintenance schedule with risk scores for `wo-2024-445`
   - **WorkOrders** container: Work order `wo-2024-445` status should be 'Scheduled', `wo-2024-456` should be 'Ready'
   - **PartsOrders** container: May be empty if no parts were needed



## Conclusion

By completing this challenge, you have built two specialized AI agents with memory capabilities that work together to deliver predictive maintenance and automated parts ordering. You've learned how to:

✅ Create persistent agents programmatically in Microsoft Foundry  
✅ Enable thread-based conversation memory  
✅ Integrate agents with Cosmos DB for data access  
✅ Orchestrate multi-agent workflows  
✅ Handle deployment updates and agent management  
✅ Build AI-powered decision systems for industrial IoT scenarios  

These agents demonstrate how AI can optimize maintenance scheduling and supply chain operations by:
- Predicting equipment failures before they occur
- Optimizing maintenance windows for minimal production impact
- Automating inventory management and supplier selection
- Maintaining context across conversations for better decision-making

## Learn More

- [Microsoft Foundry Documentation](https://learn.microsoft.com/azure/ai-studio/)
- [Azure AI Agents (Persistent Agents)](https://learn.microsoft.com/azure/ai-foundry/agents/)
- [Agent Memory Concepts](https://learn.microsoft.com/azure/ai-foundry/agents/concepts/what-is-memory)
- [Azure AI Projects SDK for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/ai.projects-readme)
- [Azure Cosmos DB .NET SDK](https://learn.microsoft.com/azure/cosmos-db/nosql/sdk-dotnet-v3)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [.NET 10 Documentation](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10)

