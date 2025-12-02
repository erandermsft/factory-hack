#!/bin/bash

# Tire Factory Data Seeding Script
# This script seeds Cosmos DB with initial data for the tire manufacturing factory

set -e

echo "=========================================="
echo "Tire Factory Data Seeding Script"
echo "=========================================="

# Check if required environment variables are set
if [ -z "$COSMOS_ENDPOINT" ] || [ -z "$COSMOS_KEY" ]; then
    echo "Error: COSMOS_ENDPOINT and COSMOS_KEY environment variables must be set"
    exit 1
fi

# Install Azure CLI Cosmos DB extension if not already installed
echo "Checking Azure CLI Cosmos DB extension..."
az extension add --name cosmosdb-preview --yes --only-show-errors 2>/dev/null || true

# Parse Cosmos DB account name from endpoint
COSMOS_ACCOUNT=$(echo $COSMOS_ENDPOINT | sed 's/https:\/\///' | sed 's/\.documents\.azure\.com.*//')
echo "Cosmos DB Account: $COSMOS_ACCOUNT"

# Database and container names
DATABASE_NAME="FactoryOpsDB"
MACHINES_CONTAINER="Machines"
THRESHOLDS_CONTAINER="Thresholds"
TELEMETRY_CONTAINER="Telemetry"
KNOWLEDGE_CONTAINER="KnowledgeBase"
PARTS_CONTAINER="PartsInventory"
TECHNICIANS_CONTAINER="Technicians"
WORKORDERS_CONTAINER="WorkOrders"

echo ""
echo "Creating database: $DATABASE_NAME"
az cosmosdb sql database create \
    --account-name "$COSMOS_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DATABASE_NAME" \
    --only-show-errors || echo "Database already exists"

echo ""
echo "Creating containers..."

# Create Machines container
az cosmosdb sql container create \
    --account-name "$COSMOS_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --database-name "$DATABASE_NAME" \
    --name "$MACHINES_CONTAINER" \
    --partition-key-path "/type" \
    --throughput 400 \
    --only-show-errors || echo "$MACHINES_CONTAINER container already exists"

# Create Thresholds container
az cosmosdb sql container create \
    --account-name "$COSMOS_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --database-name "$DATABASE_NAME" \
    --name "$THRESHOLDS_CONTAINER" \
    --partition-key-path "/machineType" \
    --throughput 400 \
    --only-show-errors || echo "$THRESHOLDS_CONTAINER container already exists"

# Create Telemetry container with TTL enabled
az cosmosdb sql container create \
    --account-name "$COSMOS_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --database-name "$DATABASE_NAME" \
    --name "$TELEMETRY_CONTAINER" \
    --partition-key-path "/machineId" \
    --throughput 400 \
    --ttl 2592000 \
    --only-show-errors || echo "$TELEMETRY_CONTAINER container already exists"

# Create KnowledgeBase container
az cosmosdb sql container create \
    --account-name "$COSMOS_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --database-name "$DATABASE_NAME" \
    --name "$KNOWLEDGE_CONTAINER" \
    --partition-key-path "/machineType" \
    --throughput 400 \
    --only-show-errors || echo "$KNOWLEDGE_CONTAINER container already exists"

# Create PartsInventory container
az cosmosdb sql container create \
    --account-name "$COSMOS_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --database-name "$DATABASE_NAME" \
    --name "$PARTS_CONTAINER" \
    --partition-key-path "/category" \
    --throughput 400 \
    --only-show-errors || echo "$PARTS_CONTAINER container already exists"

# Create Technicians container
az cosmosdb sql container create \
    --account-name "$COSMOS_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --database-name "$DATABASE_NAME" \
    --name "$TECHNICIANS_CONTAINER" \
    --partition-key-path "/department" \
    --throughput 400 \
    --only-show-errors || echo "$TECHNICIANS_CONTAINER container already exists"

# Create WorkOrders container
az cosmosdb sql container create \
    --account-name "$COSMOS_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --database-name "$DATABASE_NAME" \
    --name "$WORKORDERS_CONTAINER" \
    --partition-key-path "/status" \
    --throughput 400 \
    --only-show-errors || echo "$WORKORDERS_CONTAINER container already exists"

echo ""
echo "Containers created successfully!"

# Function to upload data to Cosmos DB
upload_data() {
    local container=$1
    local file=$2
    local partition_key=$3
    
    echo ""
    echo "Uploading data to $container from $file..."
    
    # Read JSON file and upload each item
    items=$(cat "$file" | jq -c '.[]')
    count=0
    
    while IFS= read -r item; do
        # Extract partition key value
        pk_value=$(echo "$item" | jq -r ".$partition_key")
        
        # Upload to Cosmos DB using Data Plane API
        curl -s -X POST \
            "$COSMOS_ENDPOINT/dbs/$DATABASE_NAME/colls/$container/docs" \
            -H "Authorization: $COSMOS_KEY" \
            -H "Content-Type: application/json" \
            -H "x-ms-documentdb-partitionkey: [\"$pk_value\"]" \
            -H "x-ms-version: 2018-12-31" \
            -d "$item" > /dev/null
        
        ((count++))
    done <<< "$items"
    
    echo "Uploaded $count items to $container"
}

# Upload all data files
echo ""
echo "=========================================="
echo "Uploading Data Files"
echo "=========================================="

# Define data directory
DATA_DIR="$(dirname "$0")/../data"

# Upload each data file
upload_data "$MACHINES_CONTAINER" "$DATA_DIR/machines.json" "type"
upload_data "$THRESHOLDS_CONTAINER" "$DATA_DIR/thresholds.json" "machineType"
upload_data "$TELEMETRY_CONTAINER" "$DATA_DIR/telemetry-samples.json" "machineId"
upload_data "$KNOWLEDGE_CONTAINER" "$DATA_DIR/knowledge-base.json" "machineType"
upload_data "$PARTS_CONTAINER" "$DATA_DIR/parts-inventory.json" "category"
upload_data "$TECHNICIANS_CONTAINER" "$DATA_DIR/technicians.json" "department"
upload_data "$WORKORDERS_CONTAINER" "$DATA_DIR/work-orders.json" "status"

echo ""
echo "=========================================="
echo "Data Seeding Complete!"
echo "=========================================="
echo ""
echo "Summary:"
echo "  Database: $DATABASE_NAME"
echo "  Containers: 7"
echo "  Cosmos DB Endpoint: $COSMOS_ENDPOINT"
echo ""
echo "You can now start using the tire factory data!"
