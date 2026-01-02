# Challenge 3: Maintenance Scheduler & Parts Ordering Agents

**Expected Duration:** 60 minutes

## 🚀 Quick Start (Python Implementation)

This challenge is implemented in **Python** using the **Microsoft Agent Framework** with Azure AI Foundry integration.

### Files in this Challenge

- `maintenance_scheduler.py` - Self-contained predictive maintenance scheduling agent
- `parts_ordering.py` - Self-contained parts ordering automation agent
- `README.md` - This file

### Installation

```bash
# From the workspace root:
cd /workspaces/factory-ops-hack

# Install dependencies (--pre flag required while in preview)
pip install --pre -r requirements.txt

# Set up environment variables
cp .env.example .env
# Edit .env with your credentials

# Authenticate with Azure
az login
```

### Run the Agents

```bash
# Navigate to challenge-3
cd challenge-3

# Run Maintenance Scheduler Agent
python maintenance_scheduler.py wo-2024-445

# Run Parts Ordering Agent  
python parts_ordering.py wo-2024-456
```

**Note:** The agents will automatically register themselves in the Azure AI Foundry portal when they run. You can view them at:
- **Portal URL**: https://ai.azure.com
- Navigate to your project → Build → Agents/Assistants
- Look for: `MaintenanceSchedulerAgent` and `PartsOrderingAgent`

---

## Introduction

In this challenge, you'll build two specialized AI agents that work together to optimize factory operations through intelligent maintenance scheduling and automated supply chain management:

- **Maintenance Scheduler Agent**: Finds optimal maintenance windows that minimize production disruption while ensuring equipment reliability. It analyzes production schedules, resource availability, and operational constraints to recommend the perfect timing for scheduled maintenance activities.

- **Parts Ordering Agent**: Monitors inventory levels, evaluates supplier performance, and automates parts ordering to ensure required components are available when needed. It optimizes order timing, supplier selection, and delivery schedules to support maintenance operations.


### What is Agent Memory?

In this challenge, we implement **chat history memory** using the Microsoft Agent Framework pattern. This allows agents to maintain context across multiple interactions by storing conversation history in Cosmos DB.

**Chat History Memory** stores the conversation messages (both user and assistant) for each entity:
- The **Maintenance Scheduler Agent** maintains separate chat histories for each machine, allowing it to learn scheduling preferences, production patterns, and optimal maintenance windows over time
- The **Parts Ordering Agent** maintains separate chat histories for each work order, helping it learn from past supplier performance and ordering decisions

This implementation follows the **AgentWithMemory_Step01_ChatHistoryMemory** pattern from the Microsoft Agent Framework, providing persistent context without requiring complex vector embeddings or portal-managed threads.

### How Memory Works in Our Agents

Our agents use **chat history memory** stored in Cosmos DB, following the Microsoft Agent Framework's memory pattern:

#### Chat History Memory (Conversation Context)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    MAINTENANCE SCHEDULER AGENT MEMORY                       │
│                    (Cosmos DB ChatHistories Container)                      │
└─────────────────────────────────────────────────────────────────────────────┘

  Session 1              Session 2              Session 3
  ─────────              ─────────              ─────────
     │                       │                       │
     ▼                       ▼                       ▼
┌─────────────────────────────────────────────────────────────┐
│  Chat History: Machine-001 (Last 10 messages)               │
│  ───────────────────────────────────────────────────────   │
│  User: "Find maintenance window for Machine-001"            │
│  Agent: "Optimal window: Sat 3AM-7AM, 0 production impact"  │
│  User: "What about weekday options?"                        │
│  Agent: "Tuesday 11PM-3AM: 15% production, saves $2K"       │
│  User: "Schedule for Saturday"                              │
│  Agent: "Confirmed: Sat 3AM, technician assigned, parts OK" │
└─────────────────────────────────────────────────────────────┘
                         ▲
                         │
                Cosmos DB (JSON)
              {id, machineId, history}

Example document in ChatHistories container:
```json
{
  "id": "machine-001-history",
  "entityId": "machine-001",
  "entityType": "machine",
  "purpose": "maintenance_scheduling",
  "historyJson": "[{\"toRole\":\"user\",\"content\":\"Find optimal maintenance window for Machine-001\"},{\"toRole\":\"assistant\",\"content\":\"Optimal window: Saturday 3AM-7AM. Zero production impact, technicians available, estimated 4hr downtime.\"},{\"toRole\":\"user\",\"content\":\"What about weekday options?\"},{\"toRole\":\"assistant\",\"content\":\"Alternative: Tuesday 11PM-3AM affects 15% production but saves $2K in weekend premium costs.\"}]",
  "updatedAt": "2025-12-20T10:30:00Z"
}
```

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      PARTS ORDERING AGENT MEMORY                            │
│                    (Cosmos DB ChatHistories Container)                      │
└─────────────────────────────────────────────────────────────────────────────┘

  Session 1              Session 2              Session 3
  ─────────              ─────────              ─────────
     │                       │                       │
     ▼                       ▼                       ▼
┌─────────────────────────────────────────────────────────────┐
│  Chat History: WO-2024-445 (Last 10 messages)               │
│  ───────────────────────────────────────────────────────    │
|  User: "Order parts from Supplier-A"                        │
│  Agent: "Ordered Belt-B2000, ETA: 5 days"                   │
│  User: "Update: Supplier-A delayed to 8 days"               │
│  Agent: "Noted: Supplier-A reliability decreased"           │
│  User: "Order same part again"                              │
│  Agent: "Recommending Supplier-B (better track record)"     │
└─────────────────────────────────────────────────────────────┘
                         ▲
                         │
                Cosmos DB (JSON)
           {id, workOrderId, history}
```

**Chat History Memory Benefits:**
- **Persistent Context**: Chat history survives across sessions and application restarts
- **Entity-Specific Intelligence**: Each machine or work order maintains its own conversation history
- **Simple & Reliable**: Direct message storage in Cosmos DB - no complex embeddings or indexing
- **Token-Efficient**: Stores only last 10 messages per entity to manage context window

### How It Works

1. **First request** for Machine-001 → Creates empty chat history, processes request, saves conversation to Cosmos DB `ChatHistories` container
2. **Second request** for Machine-001 → Retrieves previous messages, adds to context, processes request, saves updated history
3. **Request** for Machine-002 → Creates separate chat history, independent from Machine-001
4. **Service restart** → Chat histories persist in Cosmos DB, no memory lost

## Agent Architecture

Both agents are **self-contained Python files** that include:
- Data models (dataclasses)
- Cosmos DB service layer
- AI agent logic with Microsoft Agent Framework
- Portal registration using Azure AI Projects SDK
- Main execution workflow

### Key Features

✅ **Portal Registration**: Agents automatically register in Azure AI Foundry portal with versioning  
✅ **Chat History Memory**: Conversation context persisted in Cosmos DB per entity (machine/work order)  
✅ **Self-Contained**: No imports between files - each agent is fully independent  
✅ **Azure Authentication**: Uses `DefaultAzureCredential` for seamless Azure authentication  
✅ **Production-Ready**: Error handling, async/await, proper resource cleanup  

## Running the Maintenance Scheduler Agent

```bash
cd /workspaces/factory-ops-hack/challenge-3
python maintenance_scheduler.py wo-2024-445
```

**Expected Output:**
1. ✓ Registers agent in Azure AI Foundry portal (if not already registered)
2. ✓ Retrieves work order from Cosmos DB
3. ✓ Analyzes historical maintenance data
4. ✓ Checks available maintenance windows (14-day window)
5. ✓ Runs AI predictive analysis with chat history context
6. ✓ Generates maintenance schedule with risk scoring
7. ✓ Saves schedule to Cosmos DB
8. ✓ Updates work order status to 'Scheduled'

**What the agent does:**
- Analyzes production schedules and identifies low-impact periods
- Evaluates historical maintenance patterns for the machine
- Calculates risk scores (0-100) and failure probability
- Recommends actions: IMMEDIATE, URGENT, SCHEDULED, or MONITOR
- Maintains chat history per machine for contextual learning
- Balances urgency against production impact

**Sample Output:**
```
=== Predictive Maintenance Agent ===

1. Retrieving work order...
   ✓ Work Order: wo-2024-445
   Machine: machine-001
   Priority: high

2. Analyzing historical maintenance data...
   ✓ Found 0 historical maintenance records

3. Checking available maintenance windows...
   ✓ Found 14 available windows in next 14 days

4. Running AI predictive analysis...
   Using persistent chat history for machine: machine-001
   ✓ Analysis complete!

=== Predictive Maintenance Schedule ===
Schedule ID: sched-1767354115.311513
Machine: machine-001
Scheduled Date: 2026-01-03 22:00
Window: 22:00 - 06:00
Production Impact: Low
Risk Score: 85/100
Failure Probability: 70.0%
Recommended Action: URGENT

5. Saving maintenance schedule...
   ✓ Schedule saved to Cosmos DB

6. Updating work order status...
   ✓ Work order status updated to 'Scheduled'

✓ Predictive Maintenance Agent completed successfully!
```

## Running the Parts Ordering Agent

```bash
cd /workspaces/factory-ops-hack/challenge-3
python parts_ordering.py wo-2024-456
```

**Expected Output:**
1. ✓ Registers agent in Azure AI Foundry portal (if not already registered)
2. ✓ Retrieves work order from Cosmos DB
3. ✓ Checks inventory status for required parts
4. ✓ Identifies parts needing ordering
5. ✓ Finds available suppliers from SCM system
6. ✓ Generates optimized parts order using AI
7. ✓ Saves order to Cosmos DB
8. ✓ Updates work order status

**What the agent does:**
- Checks current inventory levels vs reorder points
- Evaluates supplier reliability, lead time, and costs
- Generates optimal parts orders with supplier selection
- Maintains chat history per work order for learning
- Calculates expected delivery dates
- Optimizes order costs while meeting urgency requirements


**How it works:**
1. First request for Machine-001 → Creates empty chat history, processes request, saves messages to Cosmos DB `ChatHistories` container
2. Second request for Machine-001 → Retrieves previous messages, adds to context, processes request, saves updated history
3. Request for Machine-002 → Creates separate chat history, independent from Machine-001
4. Service restart → Chat histories persist in Cosmos DB, no memory lost

**Benefits of this approach:**
- **Simple & Reliable**: Direct JSON storage in Cosmos DB - no complex indexing or vector embeddings
- **Survives restarts**: Chat history persists even when the application restarts
- **Token-Efficient**: Stores only last 10 messages per entity to keep context window manageable
- **Entity-Specific**: Each machine/work order maintains its own conversation context
- **Transparent**: Easy to inspect, debug, and understand stored conversation data

## Viewing Agents in Azure AI Foundry Portal

After running the agents, you can view them in the Azure AI Foundry portal:

1. **Navigate to**: https://ai.azure.com
2. **Select your project**: Look for your project (e.g., `msagthack-aiproject-...`)
3. **Go to**: Build → Agents (or Assistants)
4. **You should see**:
   - `MaintenanceSchedulerAgent` - Predictive maintenance scheduling
   - `PartsOrderingAgent` - Parts ordering automation

Each agent includes:
- Model configuration (gpt-4.1-mini)
- System instructions
- Version metadata
- Creation timestamp


## Learn More

Congratulations! You've successfully deployed two AI agents using the Microsoft Agent Framework. You've learned how to:

✅ **Build self-contained agents** - Complete agent logic in single Python files  
✅ **Register agents in Azure AI Foundry** - Programmatic portal registration with versioning  
✅ **Implement chat history memory** - Persistent conversation context in Cosmos DB  
✅ **Use Microsoft Agent Framework** - Modern agent architecture with `ChatAgent`  
✅ **Integrate with Azure services** - Cosmos DB, Azure AI Foundry, Azure OpenAI  
✅ **Manage conversation context** - Entity-specific memory (per machine/work order)  
✅ **Build production-ready agents** - Error handling, async/await, resource cleanup  

These agents demonstrate how AI can optimize factory operations by:
- Finding optimal maintenance windows that minimize production disruption
- Analyzing historical patterns and risk factors for intelligent scheduling
- Automating inventory management and supplier selection
- Maintaining conversation context for contextual decision-making
- Learning from previous interactions to improve recommendations