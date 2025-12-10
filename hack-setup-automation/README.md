# Factory Operations Hack - Setup Automation

Complete automation scripts for preparing and deploying the Factory Operations Hack environment where **each participant gets their own isolated resource group** with full Azure infrastructure.

## 📋 Prerequisites

- Azure CLI installed (`az --version`)
- Logged in to Azure (`az login`)
- Permissions:
  - User Administrator or Global Administrator (for user creation)
  - Contributor or Owner (for resource deployment)
- Tools: `jq`, `openssl` (usually pre-installed)

---

## 🚀 Quick Start (4 Steps - Follow This Order!)

### Step 1: Create Coach Users (< 1 minute)

```bash
cd hack-setup-automation
./create-coach-users.sh --coach-prefix factorycoach --count 1
```

**Output**: `coach-credentials.txt`

Creates coaches with Owner role at subscription level to manage the hack environment.

---

### Step 2: Create Hack Participant Users (< 2 minutes)

```bash
./create-hack-users.sh --user-prefix hackuser --count 5
```

**Output**: 
- `hack-user-credentials.txt`
- `hack-user-credentials.csv` ← **Save this file!**

**What it does:**
- Creates Azure AD users: `hackuser1@domain.com`, `hackuser2@domain.com`, etc.
- Generates secure random passwords
- Prepares resource group names: `hackuser1-rg`, `hackuser2-rg`, etc.
- **Does NOT create resource groups yet** - just the users

**CSV Example:**
```csv
Username,Password,UserID,ResourceGroup
hackuser1@contoso.com,SecurePass123,abc-123,hackuser1-rg
hackuser2@contoso.com,SecurePass456,def-456,hackuser2-rg
```

---

### Step 3: Deploy Infrastructure for Each User (15-30 min per user)

```bash
./deploy-infrastructure.sh --csv-file hack-user-credentials.csv
```

**Options:**
```bash
# Sequential (default) - see progress for each deployment
./deploy-infrastructure.sh --csv-file hack-user-credentials.csv

# Parallel - deploy all at once (faster but less visibility)
./deploy-infrastructure.sh --csv-file hack-user-credentials.csv --parallel

# Multi-region distribution - distribute users across regions (avoid quota limits)
./deploy-infrastructure.sh --csv-file hack-user-credentials.csv --region-list "swedencentral,eastus,westeurope"

# Multi-region + Parallel - fastest for large deployments
./deploy-infrastructure.sh --csv-file hack-user-credentials.csv --region-list "swedencentral,eastus,westeurope" --parallel
```

**What it does for each user:**
1. Creates resource group (e.g., `hackuser1-rg`)
2. Deploys complete Azure infrastructure in that resource group
3. Assigns user as **Contributor** on their resource group
4. Assigns additional roles: Azure AI Developer, Cognitive Services User, Search Service Contributor

**Deployed resources per user:**
- Storage Account
- Cosmos DB
- Azure AI Foundry (Hub + Project)
- Azure Cognitive Search
- Container Registry (ACR)
- API Management
- Container Apps
- Application Insights
- Log Analytics Workspace
- Content Safety Service

**Output**: `deployment-hackuser1-rg.json`, `deployment-hackuser2-rg.json`, etc.

**Time estimate:**
- 5 users × 20 minutes = ~100 minutes sequential
- 5 users in parallel = ~20-30 minutes total

---

### Step 4: Seed Database (2-5 min per user)

```bash
./post-deployment.sh --csv-file hack-user-credentials.csv
```

**What it does for each resource group:**
- Runs `seed-data.sh` to populate Cosmos DB with sample data:
  - Machines
  - Thresholds
  - Telemetry samples
  - Knowledge base
  - Parts inventory
  - Technicians
  - Work orders

**Options:**
```bash
# Process all teams
./post-deployment.sh --csv-file hack-user-credentials.csv

# Process single team (if needed)
./post-deployment.sh --resource-group hackuser1-rg
```

---

## ✅ Verification

After all steps complete:

```bash
# List all hack resource groups
az group list --query "[?contains(name, 'hackuser')]" --output table

# Check resources in a team's RG
az resource list --resource-group hackuser1-rg --output table

# Verify Cosmos DB was created
az cosmosdb list --resource-group hackuser1-rg --output table
```

---

## 📤 Distribute Credentials to Teams

Share the CSV file with participants. Each row contains:
- Username and password
- User ID
- Resource group name

**Each team gets:**
- Login: `hackuser1@domain.com`
- Password: [from CSV]
- Resource Group: `hackuser1-rg`
- Permissions: Contributor on their RG only
- Resources: Complete isolated environment

---

## 🏗️ Architecture

```
Team 1 (hackuser1@contoso.com)
  └── hackuser1-rg
       ├── Storage Account
       ├── Cosmos DB
       ├── AI Foundry (Hub + Project)
       ├── Cognitive Search
       ├── Container Registry
       ├── API Management
       ├── Container Apps
       ├── Application Insights
       └── Log Analytics

Team 2 (hackuser2@contoso.com)
  └── hackuser2-rg
       ├── Storage Account
       ├── Cosmos DB
       └── ... (same resources)

... and so on for each team
```

**Key Features:**
- ✅ Complete isolation - each team has their own resource group
- ✅ No interference - teams can't see or modify other teams' resources
- ✅ Full permissions - Contributor role on their own RG
- ✅ Easy cleanup - delete one RG to remove a team's entire environment

---

## 📚 Script Reference

| Script | Purpose | Duration |
|--------|---------|----------|
| `create-coach-users.sh` | Create coach users | < 1 min |
| `create-hack-users.sh` | Create participant users | < 2 min |
| `deploy-infrastructure.sh` | Deploy resources per user | 15-30 min each |
| `post-deployment.sh` | Seed Cosmos DB data | 2-5 min each |

### create-coach-users.sh

Creates coach users with **Owner** role at subscription level.

```bash
./create-coach-users.sh [OPTIONS]

Options:
  --coach-prefix PREFIX   Prefix for coach usernames (default: factorycoach)
  --count COUNT           Number of coach users (default: 1)
  --domain DOMAIN         Custom domain (optional)
```

### create-hack-users.sh

```bash
./create-hack-users.sh [OPTIONS]

Options:
  --user-prefix PREFIX    Prefix for hack usernames (default: hackuser)
  --count COUNT           Number of users (default: 5)
  --domain DOMAIN         Custom domain (optional)
```

### deploy-infrastructure.sh

```bash
./deploy-infrastructure.sh [OPTIONS]

Options:
  --csv-file FILE            CSV file with user credentials (required)
  --location LOCATION        Azure region (default: swedencentral)
  --region-list REGIONS      Comma-separated regions to distribute deployments
  --parallel                 Deploy all RGs in parallel
```

### post-deployment.sh

```bash
./post-deployment.sh [OPTIONS]

Options:
  --resource-group RG     Single resource group
  --csv-file FILE         Process all from CSV
  --skip-seed             Skip database seeding
```

---

## ⏱️ Time Estimates

**For 5 teams:**

| Approach | Duration |
|----------|----------|
| Sequential | 1.5-3 hours |
| Parallel (Step 3 with --parallel) | 30-60 minutes |

**Breakdown:**
- Step 1: < 1 min
- Step 2: < 2 min
- Step 3: 15-30 min × users (or parallel: 15-30 min total)
- Step 4: 2-5 min × users

---

## 🗑️ Cleanup After Hack

### Remove All Team Resources

```bash
# List all hack resource groups
az group list --query "[?contains(name, 'hackuser')].name" -o tsv

# Delete all hack resource groups
az group list --query "[?contains(name, 'hackuser')].name" -o tsv | \
  xargs -I {} az group delete --name {} --yes --no-wait
```

### Remove Users

```bash
# List all hack users
az ad user list --filter "startswith(userPrincipalName,'hackuser')" \
  --query "[].userPrincipalName" -o tsv

# Delete users
az ad user list --filter "startswith(userPrincipalName,'hackuser')" \
  --query "[].userPrincipalName" -o tsv | \
  xargs -I {} az ad user delete --id {}
```

---

## 🐛 Troubleshooting

### "Cannot find CSV file"
Use the filename `hack-user-credentials.csv` from Step 2 output.

### "User already exists"
Script will skip existing users automatically.

### "Deployment timeout"
- Check Azure status: https://status.azure.com
- Try deploying single RG manually
- Consider using `--parallel` for faster deployment

### "Permission denied"
Ensure you have:
- User Administrator role (for user creation)
- Contributor/Owner role (for resource deployment)

### "Template validation failed"
Ensure `../challenge-0/infra/azuredeploy.json` exists and is valid.

### "InsufficientQuota" for Azure OpenAI
Azure OpenAI has per-region quota limits. Solutions:
1. **Use multi-region distribution** (recommended for 10+ users):
   ```bash
   ./deploy-infrastructure.sh --csv-file hack-user-credentials.csv \
     --region-list "swedencentral,eastus,westeurope"
   ```
   This distributes users round-robin across regions to avoid exhausting quota in one region.

2. **Request quota increase**: Open Azure Portal → Quotas → Request increase

3. **Deploy in smaller batches**: Deploy 5-10 users at a time, wait for quota to refresh

---

## 📊 Cost Estimate (Approximate)

**Per team per month:**
- Storage: $5-20
- Cosmos DB: $25-100
- AI Foundry: $50-200
- Cognitive Search: $75-250
- Container Registry: $5-20
- API Management: $50-200
- Container Apps: $10-50
- App Insights: $5-25
- Log Analytics: $10-50

**Total per team/month: $235-915**

**For 5 teams: $1,175-4,575/month**

💡 **Tip**: Delete resources immediately after hack to minimize costs!

---

## 💡 Pro Tips

1. **Test First**: Deploy for 1 user first to verify everything works
2. **Use Multi-Region for Scale**: For 10+ users, use `--region-list` to avoid quota issues
3. **Use Parallel**: Add `--parallel` flag to deploy all teams simultaneously
4. **Save CSV**: Keep the credentials CSV safe - you'll need it for post-deployment
5. **Monitor Costs**: Each team's RG is independent - monitor spending per RG
6. **Name Consistency**: Use consistent prefixes for easy management

### 🌍 Recommended Region Combinations

For 30 users, distribute across 3+ regions:
```bash
# Option 1: Europe + US East
--region-list "swedencentral,eastus,westeurope"

# Option 2: Europe + US (broader distribution)
--region-list "swedencentral,eastus,westeurope,northeurope,westus2"

# Option 3: Global distribution
--region-list "swedencentral,eastus,westeurope,southeastasia,australiaeast"
```

Each region gets ~10 users (30 ÷ 3 = 10), staying well below quota limits.

---

## 📞 Monitoring & Support

### Check Deployment Status

```bash
# Check specific deployment
az deployment group show \
  --name <deployment-name> \
  --resource-group hackuser1-rg \
  --query properties.provisioningState

# View resource group activity
az monitor activity-log list \
  --resource-group hackuser1-rg \
  --output table

# List team's resources
az resource list \
  --resource-group hackuser1-rg \
  --output table
```

### Monitor All Teams

```bash
# List all hack resource groups
az group list --query "[?contains(name, 'hackuser')]" --output table

# Check resource counts per team
for rg in $(az group list --query "[?contains(name, 'hackuser')].name" -o tsv); do
  count=$(az resource list --resource-group $rg --query "length([])" -o tsv)
  echo "$rg: $count resources"
done
```

---

## 🎯 What Each Team Gets

✅ **Dedicated Resource Group** (e.g., `hackuser1-rg`, `hackuser2-rg`)  
✅ **Complete Infrastructure Isolation**  
✅ **Full set of Azure resources** per team  
✅ **Contributor permissions** on their resource group  
✅ **Seeded Cosmos DB** with sample factory data  
✅ **Environment file** with all connection strings and keys  

---

## 📁 Generated Output Files

After running all scripts:

```
hack-setup-automation/
├── coach-credentials.txt
├── hack-user-credentials.txt
├── hack-user-credentials.csv  ← Share this
├── deployment-hackuser1-rg.json
├── deployment-hackuser2-rg.json
└── ... (one deployment file per team)
```

Plus in `../challenge-0/`:
```
challenge-0/
└── .env  (environment variables for last processed team)
```

---

## 🔗 Additional Resources

- [Azure CLI Documentation](https://docs.microsoft.com/cli/azure/)
- [Azure Resource Manager Templates](https://docs.microsoft.com/azure/azure-resource-manager/templates/)

---

## ✨ Summary

This automation creates a **production-ready hack environment** where each participant gets:
- Their own isolated Azure resource group
- Complete set of Azure AI and data services
- Seeded database with factory operations sample data
- Full Contributor access to their resources
- Zero ability to interfere with other teams

Perfect for hackathons, workshops, and training events where you need multiple isolated environments quickly!

---

**Ready to start? Begin with Step 1!** 🚀
