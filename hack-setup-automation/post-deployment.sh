#!/bin/bash
#
# This script seeds Cosmos DB with sample data for the Factory Operations Hack
#
# Usage: ./post-deployment.sh --resource-group RG_NAME
#        ./post-deployment.sh --csv-file CREDENTIALS_FILE

set -e

# Default values
RESOURCE_GROUP=""
CSV_FILE=""

# Parse command line arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --resource-group) RESOURCE_GROUP="$2"; shift ;;
        --csv-file) CSV_FILE="$2"; shift ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --resource-group RG     Name of a single resource group"
            echo "  --csv-file FILE         CSV file with user credentials (process all)"
            echo "  --help                  Show this help message"
            echo ""
            echo "Examples:"
            echo "  # Single resource group"
            echo "  $0 --resource-group hackuser1-rg"
            echo ""
            echo "  # All resource groups from CSV"
            echo "  $0 --csv-file hack-user-credentials.csv"
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

# Determine mode: single RG or CSV file
if [ -n "$CSV_FILE" ]; then
    # CSV mode - process all resource groups
    if [ ! -f "$CSV_FILE" ]; then
        echo "❌ CSV file not found: $CSV_FILE"
        exit 1
    fi
    
    MODE="csv"
    RG_COUNT=$(tail -n +2 "$CSV_FILE" | wc -l)
    
    echo ""
    echo "=========================================="
    echo "Factory Operations Hack - Data Seeding"
    echo "=========================================="
    echo "Processing $RG_COUNT resource groups from CSV"
    echo "=========================================="
    echo ""
elif [ -n "$RESOURCE_GROUP" ]; then
    # Single RG mode
    if ! az group show --name "$RESOURCE_GROUP" &>/dev/null; then
        echo "❌ Resource group '$RESOURCE_GROUP' not found"
        exit 1
    fi
    
    MODE="single"
    
    echo ""
    echo "=========================================="
    echo "Factory Operations Hack - Data Seeding"
    echo "=========================================="
    echo "Resource Group: $RESOURCE_GROUP"
    echo "=========================================="
    echo ""
else
    # No input provided
    echo "❌ Either --resource-group or --csv-file is required"
    echo "Use --help for usage information"
    exit 1
fi

# Function to process a single resource group
process_resource_group() {
    local RG=$1
    local TEAM_NAME=$2
    
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "🌱 Seeding: $TEAM_NAME ($RG)"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    # Seed database
    echo "🌱 Seeding Cosmos DB..."
    
    if [ -f "../challenge-0/seed-data.sh" ]; then
        cd ../challenge-0
        chmod +x ./seed-data.sh
        ./seed-data.sh --resource-group "$RG"
        
        if [ $? -eq 0 ]; then
            echo "✅ Database seeded"
        else
            echo "❌ Failed to seed database for $RG"
            cd - > /dev/null
            return 1
        fi
        cd - > /dev/null
    else
        echo "❌ seed-data.sh not found"
        return 1
    fi
    
    echo "✅ $TEAM_NAME completed successfully"
    return 0
}

# Process based on mode
if [ "$MODE" = "single" ]; then
    # Single resource group mode
    process_resource_group "$RESOURCE_GROUP" "Team"
    PROCESS_STATUS=$?
elif [ "$MODE" = "csv" ]; then
    # CSV mode - process all resource groups
    TEAM_NUMBER=1
    SUCCESSFUL=0
    FAILED=0
    
    tail -n +2 "$CSV_FILE" | while IFS=',' read -r USERNAME PASSWORD USER_ID RESOURCE_GROUP; do
        if process_resource_group "$RESOURCE_GROUP" "Team $TEAM_NUMBER ($USERNAME)"; then
            ((SUCCESSFUL++))
        else
            ((FAILED++))
        fi
        ((TEAM_NUMBER++))
    done
    
    echo ""
    echo "=========================================="
    echo "📊 Batch Processing Summary"
    echo "=========================================="
    echo "Total teams: $((TEAM_NUMBER - 1))"
    echo "Successful: $SUCCESSFUL"
    echo "Failed: $FAILED"
    echo "=========================================="
    
    PROCESS_STATUS=0
fi

echo ""
echo "=========================================="
echo "✅ Data seeding completed!"
echo "=========================================="
echo ""

if [ "$MODE" = "single" ]; then
    echo "📝 Database seeded for: $RESOURCE_GROUP"
    echo ""
    echo "🎯 Environment ready for: $RESOURCE_GROUP"
elif [ "$MODE" = "csv" ]; then
    echo "🎯 All team databases are seeded!"
    echo ""
    echo "📝 Each team has:"
    echo "   - Their own resource group"
    echo "   - Isolated Azure resources"
    echo "   - Seeded Cosmos DB with sample data"
fi

echo ""
echo "👥 Teams can now access their environments!"
echo ""
echo "💡 Useful Commands:"
if [ "$MODE" = "single" ]; then
    echo "   - View resources: az resource list --resource-group $RESOURCE_GROUP --output table"
    echo "   - View Cosmos DB: az cosmosdb list --resource-group $RESOURCE_GROUP --output table"
else
    echo "   - List all hack resource groups: az group list --query \"[?contains(name, 'hackuser')]\" --output table"
    echo "   - View team resources: az resource list --resource-group <team-rg> --output table"
fi
echo ""

exit $PROCESS_STATUS
