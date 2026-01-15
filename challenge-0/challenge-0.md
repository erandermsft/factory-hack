# Challenge 0: Environment Setup

Welcome to Challenge 0!

This challenge sets up a complete Azure environment for a **tire manufacturing factory** that produces automotive tires.

**Expected duration**: 45-60 min
**Prerequisites**:

1. **Azure Subscription** with permissions to create resources
2. **GitHub Account** to fork the repository
3. **GitHub Codespaces** access
4. **Azure CLI** (pre-installed in Codespaces)
## 🎯 Objective

- Set up the Azure infrastructure and seed initial data for the tire factory predictive maintenance multi-agent system.
- Understand the business scenario for the hackathon and what problem we are trying to solve.

## 🧭 Context and background information

[TODO: add more explanation]

### Technologies Used

- Azure Resource Manager (ARM Templates)
- Azure Cosmos DB
- Microsoft Foundry
- Azure Cognitive Search
- Azure Container Apps
- GitHub Codespaces

### Tire Manufacturing Equipment Monitored

- **Tire Curing Presses** - Vulcanize green tires into finished products
- **Tire Building Machines** - Assemble tire components on a building drum
- **Tire Extruders** - Process rubber compounds into tire components
- **Tire Uniformity Machines** - Quality control and performance testing
- **Banbury Mixers** - Mix rubber compounds with additives

### What Gets Deployed

<details>
  <summary>Azure Resources (15+ services)</summary>

  **Data & Storage:**

- Azure Cosmos DB (NoSQL database)
- Azure Storage Account
- Azure Cognitive Search

  **AI & Analytics:**

- Microsoft Foundry Hub & Project
- GPT-4.1-mini deployment
- Azure Content Safety
- Application Insights

  **Compute:**

- Azure Container Apps Environment
- Azure Container App (API)
- Azure Container Registry
- Azure API Management

  **Monitoring:**

- Log Analytics Workspace

</details>

<details>
<summary>Cosmos DB Data Model (7 Containers)</summary>

| Container | Partition Key | Purpose | Sample Count |
|-----------|--------------|---------|--------------|
| **Machines** | `/type` | Equipment definitions | 5 machines |
| **Thresholds** | `/machineType` | Operating limits | 13 thresholds |
| **Telemetry** | `/machineId` | Sensor readings | 10 samples |
| **KnowledgeBase** | `/machineType` | Troubleshooting | 10 articles |
| **PartsInventory** | `/category` | Spare parts | 16 parts |
| **Technicians** | `/department` | Maintenance staff | 6 technicians |
| **WorkOrders** | `/status` | Maintenance history | 5 work orders |

</details>

<details>
<summary>Sample Machines (5 Production Units)</summary>

1. **Tire Curing Press A1** (`machine-001`)
   - Type: `tire_curing_press`
   - Status: Operational
   - Operating Hours: 12,450
   - Cycles Completed: 45,680
   - Key Metrics: Temperature (165-175°C), Pressure (150-190 bar)

2. **Tire Building Machine B1** (`machine-002`)
   - Type: `tire_building_machine`
   - Status: Operational
   - Tires Built: 67,840
   - Key Metrics: Drum vibration, Ply tension

3. **Tire Extruder C1** (`machine-003`)
   - Type: `tire_extruder`
   - Status: Operational
   - Total Output: 1,245 tons
   - Key Metrics: Barrel temperature, Extrusion pressure

4. **Tire Uniformity Machine D1** (`machine-004`)
   - Type: `tire_uniformity_machine`
   - Status: ⚠️ Maintenance Required
   - Tires Inspected: 98,450
   - Key Metrics: Force variation, Balance

5. **Banbury Mixer E1** (`machine-005`)
   - Type: `banbury_mixer`
   - Status: Operational
   - Batches Completed: 15,670
   - Key Metrics: Mixing temperature, Power consumption

</details>

<details>
<summary>Telemetry Samples (with Anomalies)</summary>

The seeded data includes **warning conditions** to test your agents:

- 🔴 **Machine 001**: Temperature 179.2°C (⚠️ exceeds 178°C warning)
- 🔴 **Machine 002**: Drum vibration 3.2 mm/s (⚠️ exceeds 3.0 mm/s)
- 🔴 **Machine 003**: Throughput 640 kg/h (⚠️ below 650 kg/h minimum)
- �� **Machine 004**: Radial force variation 105N (⚠️ exceeds 100N)
- 🔴 **Machine 005**: Multiple warnings (temp, power, vibration)

</details>

<details>
<summary>Knowledge Base (10 Troubleshooting Sample Guides)</summary>

Sample articles include:

- Curing temperature excessive
- Building drum vibration
- Extruder barrel overheating
- High radial force variation
- Mixer vibration issues

Each article contains:

- Symptoms & possible causes
- Diagnostic steps
- Solutions & repair procedures
- Estimated repair times

</details>

<details>
<summary>Parts Inventory (16 sample Spare Parts)</summary>

Categories include:

- Bladders, seals, and heating elements
- Bearings and servo motors
- Sensors and load cells
- Extruder screws and dies
- Mixer rotor tips

Sample parts with low stock trigger reorder alerts.
</details>
<details>
<summary>Technicians (6 Specialists)</summary>

- **John Smith** - Senior Tire Equipment Technician
- **Maria Garcia** - Building Machine Specialist
- **David Lee** - Quality Systems Technician (⚠️ on assignment)
- **Sarah Johnson** - Electrical Technician
- **Michael Chen** - Mixing & Extrusion Technician
- **Jennifer Rodriguez** - Mechanical Technician

</details>

## ✅ Tasks

### Task 1: Fork & Launch Codespace

1. Fork this repository to your GitHub account
2. Open GitHub Codespaces from your fork
3. Wait for the environment to initialize

---

### Task 2: Login to Azure

```bash
az login --use-device-code
```

---

### Task 3: Deploy Resources

> [!IMPORTANT]
> Depending on the setup for the hackathon the Azure resources might already have been provisioned for you and you can then skip this step.
> Check with your hackathon coach what is applicable for you.

```bash
# Ensure you are located in challenge-0 directory 
cd challenge-0

# Make resource group name easy to identify. Use your initials or other identifier (e.g., "jd" for John Doe)
export RG_SUFFIX="<initials>"

# Set variables with your initials as suffix
export RESOURCE_GROUP="rg-tire-factory-hack-${RG_SUFFIX}"
export LOCATION="swedencentral"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Deploy infrastructure
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/azuredeploy.json \
  --parameters location=$LOCATION
```

⏱️ Deployment takes approximately 5-10 minutes

---

### Task 4: Configure Environment

```bash
# Extract connection keys
scripts/get-keys.sh --resource-group $RESOURCE_GROUP

# Verify .env file
cat ../.env
```

> [!TIP]
> Keep your `.env` file handy throughout the hackathon. Add it to `.gitignore` to avoid committing secrets!

> [!CAUTION]
>For convenience we will use key-based authentication and public network access to resources in the hack. In real world implementations you should consider stronger authentication mechanisms and additional network security.

---

### Task 5: Seed Factory Sample Data

```bash
# Export environment variables
export $(cat ../.env | xargs)

# Run data seeding script
scripts/seed-data.sh
```

---

### Task 6: Verify Deployment

```bash
# List all resources
az resource list \
  --resource-group $RESOURCE_GROUP \
  --output table

# Check Cosmos DB
az cosmosdb sql container list \
  --account-name $(az cosmosdb list -g $RESOURCE_GROUP --query "[0].name" -o tsv) \
  --resource-group $RESOURCE_GROUP \
  --database-name FactoryOpsDB \
  --output table
```

---

### Task 7 (optional): Run Sample Queries

If you want to verify or explore the seeded data, here are some sample queries you can run against the Cosmos DB.
This can be done via the Azure Portal Data Explorer. As shown below:

![Azure Portal Data Explorer](../images/dataexplorer-sample-query.png)

**Find machines with warnings in Telemetry container:**

```sql
SELECT c.machineId, c.status, c.alerts FROM c WHERE c.status = "warning"
```

**Get curing press thresholds in Thresholds container:**

```sql
SELECT c.metric, c.normalRange, c.warningThreshold, c.criticalThreshold
FROM c
WHERE c.machineType = "tire_curing_press"
```

**Find available technicians in the Technicians container with curing press skills:**

```sql
SELECT c.name, c.skills, c.availability
FROM c
WHERE ARRAY_CONTAINS(c.skills, "tire_curing_press") 
  AND c.availability = "available"
```

---

### Success Criteria

- [ ] All Azure resources deployed (15+ services)  
- [ ] `.env` file configured with connection strings  
- [ ] Cosmos DB contains 7 containers  
- [ ] **65+ data items** seeded across all containers  
- [ ] Can query machines and see telemetry warnings  
- [ ] AI Foundry project accessible with GPT-4.1-mini  
- [ ] Cognitive Search service running  

## 🛠️ Troubleshooting and FAQ

### Deployment Issues

**Problem:** ARM template deployment fails

```bash
# Check deployment errors
az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name azuredeploy \
  --query properties.error

# Register missing providers (if needed)
az provider register --namespace Microsoft.AlertsManagement
az provider register --namespace Microsoft.App
```

### Data Seeding Issues

**Problem:** Seed script fails

```bash
# Verify Cosmos DB is ready
az cosmosdb show \
  --name $COSMOS_NAME \
  --resource-group $RESOURCE_GROUP \
  --query provisioningState

# Check if containers exist
az cosmosdb sql container list \
  --account-name $COSMOS_NAME \
  --resource-group $RESOURCE_GROUP \
  --database-name FactoryOpsDB

# Re-run seed script (idempotent)
bash challenge-0/scripts/seed-data.sh
```

**Problem:** Permission denied on seed script

```bash
chmod +x challenge-0/scripts/seed-data.sh
```

### Connection Issues

**Problem:** Can't connect to Cosmos DB

```bash
# Get connection string
az cosmosdb keys list \
  --name $COSMOS_NAME \
  --resource-group $RESOURCE_GROUP \
  --type connection-strings

# Test connectivity
curl -X GET "$COSMOS_ENDPOINT" -H "Authorization: $COSMOS_KEY"
```

### Clean Up

⚠️ **Only run this at the end of the hackathon:**

```bash
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

## 🧠 Conclusion and reflection


🎉 Congratulations! Your tire factory environment is ready. You have provisioned a complete Tire Factory demo environment including

- 5 production machines with realistic specifications
- 13 operating thresholds for anomaly detection
- 10 telemetry readings (including 5 with warnings!)
- 10 troubleshooting knowledge articles
- 16 spare parts inventory items
- 6 skilled maintenance technicians
- 5 historical work orders


This forms the complete foundation for your multi-agent predictive maintenance hackathon system

Time to build some intelligent agents!

> [!IMPORTANT]
> This hackathon uses simplified authentication for learning purposes. Production systems should implement:
>
> - Managed identities instead of keys
> - Private endpoints for network security
> - Azure Key Vault for secrets management
> - RBAC for fine-grained access control

**Next step:** [Challenge 1](../challenge-1/challenge-1.md) - Building Agent Framework Agents for Anomaly Classification and Fault Diagnosis


If you want to expand your knowledge on what we-ve covered in this challenge, have a look at the content below:

- [Azure Cosmos DB Documentation](https://learn.microsoft.com/azure/cosmos-db/)
- [Microsoft Foundry](https://learn.microsoft.com/azure/ai-foundry/)
- [Azure Cognitive Search](https://learn.microsoft.com/azure/search/)
- [Tire Manufacturing Process](https://en.wikipedia.org/wiki/Tire_manufacturing)
- [Predictive Maintenance Patterns](https://learn.microsoft.com/azure/architecture/data-guide/scenarios/predictive-maintenance)

---
