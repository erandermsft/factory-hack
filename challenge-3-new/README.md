# Challenge 3 (Refactored) - Predictive Maintenance Scheduler

A refactored, modular version of the maintenance scheduler agent with improved structure, testability, and maintainability.

## 📋 What This Agent Does

The Maintenance Scheduler Agent analyzes work orders and determines the optimal time to perform maintenance by balancing equipment reliability needs against production impact.

**Agent Workflow:**
1. **Reads Work Order** from `WorkOrders` container in Cosmos DB
2. **Analyzes Historical Data** from `MaintenanceHistory` container to understand failure patterns
3. **Checks Available Windows** from `MaintenanceWindows` container to find low-impact periods
4. **Runs AI Analysis** using Microsoft Agent Framework to assess risk and recommend timing
5. **Saves Schedule** to `MaintenanceSchedules` container with risk scores and recommendations
6. **Updates Work Order** status to 'Scheduled'

### Cosmos DB Integration

The agent interacts with Azure Cosmos DB as its primary data store:

| Container | Access | Purpose |
|-----------|--------|---------|
| **WorkOrders** | Read | Get job details and required maintenance |
| **MaintenanceHistory** | Read | Analyze historical failure patterns |
| **MaintenanceWindows** | Read | Find optimal low-impact time slots |
| **MaintenanceSchedules** | Write | Save generated schedules |
| **ChatHistories** | Read/Write | Persist agent conversation history |

## 🏗️ Architecture

This refactored version separates concerns into clean, testable modules:

```
challenge-3-new/
├── models/                      # Data models
│   ├── work_order.py           # WorkOrder, RequiredPart
│   └── maintenance.py          # MaintenanceWindow, MaintenanceSchedule, MaintenanceHistory
├── services/                    # Business logic
│   ├── cosmos_db_service.py    # Database access layer
│   └── scheduler_agent.py      # AI agent logic
├── observability/              # Monitoring & tracing
│   ├── tracing.py             # OpenTelemetry setup
│   └── agent_registration.py  # Azure AI Foundry registration
├── utils/                      # Shared utilities
│   ├── datetime_helpers.py    # Date parsing
│   └── json_helpers.py        # JSON extraction
├── tests/                      # Test suite
│   ├── conftest.py            # Test configuration
│   ├── test_cosmos_service.py # Database tests
│   ├── test_scheduler_agent.py # Agent tests
│   └── fixtures/              # Test data
├── config.py                   # Configuration management
├── maintenance_scheduler.py    # Main entry point (~130 lines)
└── requirements.txt           # Dependencies
```

## ✨ Key Improvements

### 1. **Separation of Concerns**
- **Models**: Pure data classes with no business logic
- **Services**: Isolated business logic (database, AI agent)
- **Utilities**: Reusable helper functions
- **Config**: Centralized configuration management

### 2. **Testability**
- Each module can be tested independently
- Mock-friendly architecture
- Sample test suite included with pytest
- Test fixtures for common scenarios

### 3. **Maintainability**
- Single responsibility per file
- Clear dependencies between modules
- Type hints throughout
- Comprehensive docstrings

### 4. **Observability**
- Isolated tracing setup
- Separate agent registration logic
- Easy to enable/disable monitoring

## 🚀 Usage

### Installation

```bash
cd challenge-3-new
pip install -r requirements.txt
```

### Configuration

Create a `.env` file or set environment variables:

```bash
# Required
COSMOS_ENDPOINT=https://your-cosmos.documents.azure.com:443/
COSMOS_KEY=your-cosmos-key
COSMOS_DATABASE_NAME=your-database-name
FOUNDRY_PROJECT_ENDPOINT=https://your-project.openai.azure.com/

# Optional
FOUNDRY_MODEL_DEPLOYMENT_NAME=gpt-4o
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...
```

### Running the Agent

```bash
# Process a specific work order
python maintenance_scheduler.py wo-2024-468

# Process default work order
python maintenance_scheduler.py
```

### Expected Output

```
=== Predictive Maintenance Agent ===

📊 Agent Framework tracing enabled (Azure Monitor)
   Traces sent to: InstrumentationKey=...
   View in Azure AI Foundry portal: https://ai.azure.com -> Your Project -> Tracing

   Checking existing agent versions for 'MaintenanceSchedulerAgent' in portal...
   Found 1 existing versions
   Creating new version (will be version #2)...
   Registering MaintenanceSchedulerAgent in Azure AI Foundry portal...
   ✅ New version created!
   Check portal at: https://ai.azure.com

1. Retrieving work order...
   ✓ Work Order: wo-2024-468
   Machine: MACHINE-003
   Fault: Hydraulic Pressure Drop
   Priority: High

2. Analyzing historical maintenance data...
   ✓ Found 5 historical maintenance records

3. Checking available maintenance windows...
   ✓ Found 14 available windows in next 14 days

4. Running AI predictive analysis...
   Using persistent chat history for machine: MACHINE-003
   ✓ Analysis complete!

=== Predictive Maintenance Schedule ===
Schedule ID: sched-1735845678.123
Machine: MACHINE-003
Scheduled Date: 2026-01-04 22:00
Window: 22:00 - 06:00
Production Impact: Low
Risk Score: 85/100
Failure Probability: 70.0%
Recommended Action: URGENT

Reasoning:
Given the high priority work order and historical pattern of hydraulic pressure 
failures on MACHINE-003, immediate scheduling is recommended. The selected 
night window minimizes production impact while addressing the critical issue 
before potential failure.

5. Saving maintenance schedule...
   ✓ Schedule saved to Cosmos DB

6. Updating work order status...
   ✓ Work order status updated to 'Scheduled'

✓ Predictive Maintenance Agent completed successfully!
```

### What Gets Saved to Cosmos DB

**MaintenanceSchedules Container:**
```json
{
  "id": "sched-1735845678.123",
  "workOrderId": "wo-2024-468",
  "machineId": "MACHINE-003",
  "scheduledDate": "2026-01-04T22:00:00Z",
  "maintenanceWindow": {
    "id": "mw-2026-01-04-night",
    "startTime": "2026-01-04T22:00:00Z",
    "endTime": "2026-01-05T06:00:00Z",
    "productionImpact": "Low",
    "isAvailable": true
  },
  "riskScore": 85.0,
  "predictedFailureProbability": 0.70,
  "recommendedAction": "URGENT",
  "reasoning": "Given the high priority work order...",
  "createdAt": "2026-01-02T15:30:00Z"
}
```

**WorkOrders Container (Updated):**
```json
{
  "id": "wo-2024-468",
  "machineId": "MACHINE-003",
  "status": "Scheduled",  // ← Updated from "Created"
  "faultType": "Hydraulic Pressure Drop",
  "priority": "High",
  ...
}
```

## 🧪 Testing

Run the test suite:

```bash
# Run all tests
pytest

# Run with coverage
pytest --cov=. --cov-report=html

# Run specific test file
pytest tests/test_cosmos_service.py -v

# Run with async support
pytest -v --asyncio-mode=auto
```

## 📦 Module Reference

### Models

**work_order.py**
- `RequiredPart`: Parts needed for maintenance
- `WorkOrder`: Work order from Repair Planner

**maintenance.py**
- `MaintenanceWindow`: Available time slots
- `MaintenanceSchedule`: Predicted schedule output
- `MaintenanceHistory`: Historical maintenance records

### Services

**cosmos_db_service.py**
- `CosmosDbService`: Database access layer
  - `get_work_order(work_order_id)`: Retrieve work orders
  - `get_maintenance_history(machine_id)`: Get historical data
  - `get_available_maintenance_windows(days_ahead)`: Get time slots
  - `save_maintenance_schedule(schedule)`: Persist schedules
  - `update_work_order_status(work_order_id, status)`: Update status
  - `get_machine_chat_history(machine_id)`: Retrieve chat history
  - `save_machine_chat_history(machine_id, history_json)`: Save chat history

**scheduler_agent.py**
- `MaintenanceSchedulerAgent`: AI-powered scheduler
  - `predict_schedule(work_order, history, windows)`: Generate optimal schedule
  - `_build_context(work_order, history, windows)`: Build AI prompt context
  - `_save_thread_history(machine_id, thread)`: Persist conversation

### Utilities

**datetime_helpers.py**
- `parse_datetime(dt_str)`: Parse ISO datetime strings

**json_helpers.py**
- `extract_json(response)`: Extract JSON from AI responses

### Observability

**tracing.py**
- `setup_tracing(connection_string)`: Configure OpenTelemetry

**agent_registration.py**
- `register_agent(...)`: Register agent in Azure AI Foundry portal

### Configuration

**config.py**
- `Config`: Configuration dataclass
- `load_config()`: Load and validate environment variables

## 🔄 Comparison with Original

| Aspect | Original | Refactored |
|--------|----------|------------|
| **File Size** | 724 lines | ~130 lines main + modules |
| **Testability** | Difficult to test | Easy to mock and test |
| **Reusability** | Monolithic | Modular components |
| **Dependencies** | Implicit | Explicit via imports |
| **Configuration** | Scattered | Centralized in config.py |
| **Error Handling** | Mixed | Consistent per layer |

## 📝 Development Guidelines

### Adding New Features

1. **New data model**: Add to appropriate file in `models/`
2. **New database method**: Add to `CosmosDbService` in `services/`
3. **New AI capability**: Extend `MaintenanceSchedulerAgent` in `services/`
4. **New utility**: Add to appropriate file in `utils/`

### Testing New Code

1. Create test file in `tests/` matching module name
2. Use fixtures from `tests/conftest.py` and `tests/fixtures/`
3. Mock external dependencies (Cosmos DB, AI client)
4. Run tests with `pytest -v`

### Code Style

- Use type hints for all function signatures
- Add docstrings to all public methods
- Follow async/await patterns consistently
- Keep functions focused on single responsibility

## 🎯 Future Enhancements

Potential improvements for this architecture:

1. **Base Agent Class**: Create shared base for both scheduler and parts ordering agents
2. **Async Context Managers**: Make services proper async context managers
3. **Integration Tests**: Add tests with Azure emulators
4. **CLI Improvements**: Add argparse for better command-line interface
5. **Logging**: Add structured logging with proper levels
6. **Error Types**: Create custom exception hierarchy
7. **Validation**: Add pydantic models for stricter validation
8. **Caching**: Add caching layer for frequently accessed data

## 📄 License

See parent directory LICENSE file.

## 📊 Azure AI Foundry Integration

### Tracing & Observability

This refactored agent includes **Azure AI Foundry tracing** for comprehensive observability and monitoring. View detailed execution traces, performance metrics, and AI model interactions in the Azure AI Foundry portal.

**What's Included:**
- ✅ **Azure Monitor Integration** - Sends traces to Application Insights
- ✅ **AI Inference Instrumentation** - Automatically traces all AI model calls
- ✅ **OpenTelemetry Support** - Industry-standard distributed tracing
- ✅ **Graceful Fallback** - Agent works even if tracing packages aren't installed

**Viewing Traces:**
1. Navigate to: https://ai.azure.com
2. Select your project
3. Go to: **Tracing** → View traces
4. Filter by agent name: `MaintenanceSchedulerAgent`

**What You'll See in Traces:**
```
MaintenanceScheduler Trace
├─ Get Work Order (Cosmos DB)
├─ Get Maintenance History (Cosmos DB)
├─ Get Maintenance Windows (Cosmos DB)
├─ AI Prediction
│  ├─ Build Context
│  ├─ Model Call (GPT-4o)
│  │  ├─ Prompt Tokens: 1,234
│  │  ├─ Completion Tokens: 567
│  │  └─ Total Duration: 2.3s
│  └─ Parse Response
├─ Save Schedule (Cosmos DB)
└─ Update Work Order (Cosmos DB)
```

### Agent Registration

The agent **automatically registers** in Azure AI Foundry portal each time it runs:
- Checks for existing versions
- Creates a new version with incremented number
- Stores metadata and instructions
- Visible in Build → Agents section

**Version Tracking:**
- Each run creates a new version (v1, v2, v3, etc.)
- Track changes to agent behavior over time
- Compare performance across versions
- Monitor version-specific metrics

### Benefits

**For Development:**
- Debug issues faster with detailed traces
- Optimize performance by identifying slow operations
- Understand agent behavior and AI decisions

**For Production:**
- Monitor reliability and success rates
- Track token usage and costs
- Performance monitoring (latency, throughput)
- Full audit trail of AI operations

## 🎯 Key Features

✅ **Cosmos DB Integration**: Direct read/write operations with multiple containers  
✅ **Portal Registration**: Agents automatically register in Azure AI Foundry with version tracking  
✅ **Azure Monitor Tracing**: Comprehensive observability with Application Insights  
✅ **Persistent Chat History**: Maintains conversation context across runs  
✅ **Modular Architecture**: Clean separation of concerns for easy testing and maintenance  
✅ **Azure Authentication**: Uses `DefaultAzureCredential` for seamless auth  
✅ **Production-Ready**: Async/await, error handling, proper resource cleanup  
✅ **AI-Powered Decision Making**: Uses GPT-4 for intelligent risk analysis and scheduling  

## 📚 Learn More

This agent demonstrates how AI can optimize factory operations through:
- **Predictive Scheduling**: Finding optimal maintenance windows using historical data
- **Risk Assessment**: Calculating failure probability and recommending actions
- **Workflow Automation**: Updating work order status as tasks complete
- **Data-Driven Decisions**: Combining database queries with AI analysis
- **Comprehensive Monitoring**: Full visibility into agent execution and AI decisions

### Additional Resources

- [Azure AI Foundry Tracing Documentation](https://learn.microsoft.com/azure/ai-foundry/how-to/develop/trace-agents-sdk)
- [Application Insights Overview](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)
- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/azure/ai-foundry/how-to/develop/agent-framework)
- [Cosmos DB Best Practices](https://learn.microsoft.com/azure/cosmos-db/nosql/best-practice-dotnet)

---

**Note**: This refactored version maintains full compatibility with the original challenge-3 agent while providing a cleaner, more maintainable, and testable architecture.
