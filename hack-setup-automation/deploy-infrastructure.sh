#!/bin/bash
#
# This script deploys the Factory Operations Hack infrastructure to Azure
# It creates a resource group for EACH hack user and deploys all required Azure services
#
# Usage: ./deploy-infrastructure.sh --csv-file CREDENTIALS_FILE [--region-list REGIONS] [--parallel]

set -e -o pipefail

# Default values
CSV_FILE=""
LOCATION="swedencentral"
REGION_LIST=""
TEMPLATE_FILE="../challenge-0/infra/azuredeploy.json"
PARALLEL=false
BASIC_SEARCH_LIMIT=12
BASIC_SEARCH_COUNT=0

# Parse command line arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --csv-file) CSV_FILE="$2"; shift ;;
        --location) LOCATION="$2"; shift ;;
        --region-list) REGION_LIST="$2"; shift ;;
        --parallel) PARALLEL=true ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --csv-file FILE            CSV file with user credentials (required)"
            echo "  --location LOCATION        Azure region (default: swedencentral)"
            echo "  --region-list REGIONS      Comma-separated list of regions to distribute deployments"
            echo "                             Example: 'swedencentral,francecentral,germanywestcentral'"
            echo "                             Users will be round-robin distributed across regions"
            echo "  --parallel                 Deploy all resource groups in parallel (faster but less visible)"
            echo "  --help                     Show this help message"
            echo ""
            echo "Example:"
            echo "  $0 --csv-file hack-user-credentials.csv"
            echo "  $0 --csv-file hack-user-credentials.csv --region-list 'swedencentral,francecentral,germanywestcentral' --parallel"
            echo ""
            echo "Note: This will create one resource group per user from the CSV file"
            exit 0
            ;;
        *) echo "Unknown parameter: $1"; exit 1 ;;
    esac
    shift
done

# Check if user is logged in to Azure
echo "🔍 Checking Azure login status..."
if ! az account show &>/dev/null; then
    echo "❌ Not logged in to Azure. Please run 'az login' first."
    exit 1
fi

# Get subscription information
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)

# Validate CSV file
if [ -z "$CSV_FILE" ]; then
    echo "❌ CSV file is required"
    echo "Usage: $0 --csv-file <credentials-file.csv>"
    exit 1
fi

if [ ! -f "$CSV_FILE" ]; then
    echo "❌ CSV file not found: $CSV_FILE"
    exit 1
fi

# Check if template file exists
if [ ! -f "$TEMPLATE_FILE" ]; then
    echo "❌ Template file not found: $TEMPLATE_FILE"
    echo "Please ensure the challenge-0/infra/azuredeploy.json exists"
    exit 1
fi

# Read CSV and count users (excluding header)
USER_COUNT=$(tail -n +2 "$CSV_FILE" | wc -l)

if [ "$USER_COUNT" -eq 0 ]; then
    echo "❌ No users found in CSV file"
    exit 1
fi

# Parse region list if provided
if [ -n "$REGION_LIST" ]; then
    IFS=',' read -ra REGIONS <<< "$REGION_LIST"
    REGION_COUNT=${#REGIONS[@]}
    echo "🌍 Region distribution enabled: ${REGION_COUNT} regions"
else
    REGIONS=("$LOCATION")
    REGION_COUNT=1
fi

echo ""
echo "=========================================="
echo "Factory Operations Hack - Infrastructure Deployment"
echo "=========================================="
echo "Subscription: $SUBSCRIPTION_NAME"
echo "Subscription ID: $SUBSCRIPTION_ID"
if [ "$REGION_COUNT" -eq 1 ]; then
    echo "Location: $LOCATION"
else
    echo "Regions: ${REGIONS[*]}"
    echo "Distribution: Round-robin across $REGION_COUNT regions"
fi
echo "Template: $TEMPLATE_FILE"
echo "Users to deploy: $USER_COUNT"
echo "Deployment mode: $([ "$PARALLEL" = true ] && echo "Parallel" || echo "Sequential")"
echo "=========================================="
echo ""

# Function to determine Search Service SKU based on quota
get_search_sku() {
    # In parallel mode, always use standard to avoid race conditions
    if [ "$FORCE_STANDARD_SEARCH" = true ]; then
        echo "standard"
        return
    fi
    
    # Count existing basic tier search services in subscription
    local EXISTING_BASIC=$(az search service list --query "[?sku.name=='basic'] | length(@)" -o tsv 2>/dev/null || echo "0")
    
    if [ "$EXISTING_BASIC" -ge "$BASIC_SEARCH_LIMIT" ]; then
        echo "standard"
    else
        echo "basic"
    fi
}

# Function to deploy infrastructure for a single user
deploy_for_user() {
    local USERNAME=$1
    local USER_ID=$2
    local RESOURCE_GROUP=$3
    local TEAM_NUMBER=$4
    local DEPLOY_REGION=$5
    
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "🚀 Deploying for Team $TEAM_NUMBER: $USERNAME"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "Resource Group: $RESOURCE_GROUP"
    echo "Region: $DEPLOY_REGION"
    echo "User ID: $USER_ID"
    echo ""
    
    # Check if resource group already exists
    if az group show --name "$RESOURCE_GROUP" &>/dev/null; then
        echo "⏭️  Resource group '$RESOURCE_GROUP' already exists - skipping deployment"
        echo ""
        return 0
    fi
    
    # Create resource group
    echo "📦 Creating resource group '$RESOURCE_GROUP'..."
    if az group create \
        --name "$RESOURCE_GROUP" \
        --location "$DEPLOY_REGION" \
        --output none 2>&1; then
        echo "✅ Resource group created"
    else
        echo "❌ Failed to create resource group '$RESOURCE_GROUP'"
        return 1
    fi
    
    # Determine which Search Service SKU to use
    SEARCH_SKU=$(get_search_sku)
    echo "🔍 Using Search Service SKU: $SEARCH_SKU"
    
    # Deploy infrastructure
    DEPLOYMENT_NAME="factory-ops-${RESOURCE_GROUP}-$(date +%Y%m%d-%H%M%S)"
    echo "🔧 Deploying infrastructure (this will take 15-30 minutes)..."
    
    START_TIME=$(date +%s)
    
    # In parallel mode, don't save output to file
    if [ "$PARALLEL" = true ]; then
        if az deployment group create \
            --name "$DEPLOYMENT_NAME" \
            --resource-group "$RESOURCE_GROUP" \
            --template-file "$TEMPLATE_FILE" \
            --parameters location="$DEPLOY_REGION" searchServiceSku="$SEARCH_SKU" \
            --output none 2>&1; then
            
            END_TIME=$(date +%s)
            DURATION=$((END_TIME - START_TIME))
            MINUTES=$((DURATION / 60))
            SECONDS=$((DURATION % 60))
            
            echo "✅ Deployment completed in ${MINUTES}m ${SECONDS}s"
        else
            echo "❌ Deployment failed for '$RESOURCE_GROUP'"
            return 1
        fi
    else
        # Sequential mode - save deployment output to file
        if az deployment group create \
            --name "$DEPLOYMENT_NAME" \
            --resource-group "$RESOURCE_GROUP" \
            --template-file "$TEMPLATE_FILE" \
            --parameters location="$DEPLOY_REGION" searchServiceSku="$SEARCH_SKU" \
            --output json > "deployment-${RESOURCE_GROUP}.json" 2>&1; then
            
            END_TIME=$(date +%s)
            DURATION=$((END_TIME - START_TIME))
            MINUTES=$((DURATION / 60))
            SECONDS=$((DURATION % 60))
            
            echo "✅ Deployment completed in ${MINUTES}m ${SECONDS}s"
        else
            echo "❌ Deployment failed for '$RESOURCE_GROUP'"
            return 1
        fi
    fi
    
    # Assign roles to the user for their resource group
    echo "👤 Assigning permissions to user..."
    
    # Get resource group ID for scope
    RG_SCOPE=$(az group show --name "$RESOURCE_GROUP" --query id -o tsv)
    
    # Contributor role
    az role assignment create \
        --assignee "$USER_ID" \
        --role "Contributor" \
        --scope "$RG_SCOPE" \
        --output none 2>&1 && echo "   ✅ Contributor role assigned" || echo "   ⚠️  Could not assign Contributor role"
    
    # Azure AI Developer role
    az role assignment create \
        --assignee "$USER_ID" \
        --role "Azure AI Developer" \
        --scope "$RG_SCOPE" \
        --output none 2>&1 && echo "   ✅ Azure AI Developer role assigned" || echo "   ⚠️  Could not assign Azure AI Developer role"
    
    # Cognitive Services User role
    az role assignment create \
        --assignee "$USER_ID" \
        --role "Cognitive Services User" \
        --scope "$RG_SCOPE" \
        --output none 2>&1 && echo "   ✅ Cognitive Services User role assigned" || echo "   ⚠️  Could not assign Cognitive Services User role"
    
    # Search Service Contributor role
    az role assignment create \
        --assignee "$USER_ID" \
        --role "Search Service Contributor" \
        --scope "$RG_SCOPE" \
        --output none 2>&1 && echo "   ✅ Search Service Contributor role assigned" || echo "   ⚠️  Could not assign Search Service Contributor role"
    
    echo ""
    echo "✅ Team $TEAM_NUMBER deployment complete!"
    
    return 0
}

# Read CSV and deploy for each user
echo "📋 Reading user list from CSV..."
echo ""

TEAM_NUMBER=1
SUCCESSFUL_DEPLOYMENTS=0
FAILED_DEPLOYMENTS=0

# Store deployment start time
OVERALL_START=$(date +%s)

# Read CSV line by line (skip header) - use process substitution to avoid subshell
while IFS=',' read -r USERNAME PASSWORD USER_ID RESOURCE_GROUP; do
    # Calculate which region to use (round-robin distribution)
    REGION_INDEX=$(( (TEAM_NUMBER - 1) % REGION_COUNT ))
    DEPLOY_REGION="${REGIONS[$REGION_INDEX]}"
    
    if [ "$PARALLEL" = true ]; then
        if [ "$TEAM_NUMBER" -eq 1 ]; then
            echo "⚠️  Warning: Parallel mode detected. All deployments will use 'standard' tier for Search Service to avoid quota conflicts."
            echo "   If you want to optimize costs with 'basic' tier for first 12 services, use sequential mode instead."
            # In parallel mode, always use standard to avoid race conditions with quota
            export FORCE_STANDARD_SEARCH=true
        fi
        # Deploy in background for parallel execution
        deploy_for_user "$USERNAME" "$USER_ID" "$RESOURCE_GROUP" "$TEAM_NUMBER" "$DEPLOY_REGION" &
    else
        # Deploy sequentially
        if deploy_for_user "$USERNAME" "$USER_ID" "$RESOURCE_GROUP" "$TEAM_NUMBER" "$DEPLOY_REGION"; then
            SUCCESSFUL_DEPLOYMENTS=$((SUCCESSFUL_DEPLOYMENTS + 1))
        else
            FAILED_DEPLOYMENTS=$((FAILED_DEPLOYMENTS + 1))
        fi
    fi
    
    TEAM_NUMBER=$((TEAM_NUMBER + 1))
done < <(tail -n +2 "$CSV_FILE")

# If parallel mode, wait for all deployments to complete
if [ "$PARALLEL" = true ]; then
    echo ""
    echo "⏳ Waiting for all parallel deployments to complete..."
    wait
    echo "✅ All deployments finished"
fi

OVERALL_END=$(date +%s)
TOTAL_DURATION=$((OVERALL_END - OVERALL_START))
TOTAL_MINUTES=$((TOTAL_DURATION / 60))
TOTAL_SECONDS=$((TOTAL_DURATION % 60))

echo ""
echo "=========================================="
echo "📊 Deployment Summary"
echo "=========================================="
echo "Total duration: ${TOTAL_MINUTES}m ${TOTAL_SECONDS}s"
echo "Users processed: $((TEAM_NUMBER - 1))"
if [ "$PARALLEL" = false ]; then
    echo "Successful: $SUCCESSFUL_DEPLOYMENTS"
    echo "Failed: $FAILED_DEPLOYMENTS"
fi
if [ "$PARALLEL" = false ]; then
    echo ""
    echo "📁 Deployment files created:"
    ls -1 deployment-*.json 2>/dev/null | sed 's/^/   - /'
    echo ""
fi
echo "=========================================="
echo ""
echo "🎯 Next Steps:"
echo ""
echo "For each team, run post-deployment configuration:"
echo ""
if [ "$PARALLEL" = false ]; then
    echo "   ./post-deployment.sh --csv-file $CSV_FILE"
else
    echo "   ./post-deployment.sh --csv-file $CSV_FILE"
fi
echo ""
echo "Or for a single team:"
echo "   ./post-deployment.sh --resource-group hackuser1-rg"
echo ""
