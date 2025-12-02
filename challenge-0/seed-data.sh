#!/bin/bash
set -e

# Load environment variables from .env in parent directory
if [ -f ../.env ]; then
    set -a
    source ../.env
    set +a
    echo "✅ Loaded environment variables from ../.env"
else
    echo "❌ .env file not found. Please run get-keys.sh first."
    exit 1
fi

echo "🚀 Starting data seeding..."

# Install required Python packages
echo "📦 Installing required Python packages..."
pip3 install azure-cosmos --quiet

# Create Python script to handle the data import
cat > seed_data.py << 'EOF'
import json
import os
from azure.cosmos import CosmosClient, PartitionKey

def load_json_data(file_path):
    """Load data from JSON file"""
    data = []
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = json.load(f)
            # If it's already a list, use it as is
            if isinstance(content, list):
                data = content
            else:
                data = [content]
        print(f"✅ Loaded {len(data)} records from {file_path}")
        return data
    except Exception as e:
        print(f"❌ Error loading {file_path}: {e}")
        return []

def setup_cosmos_db():
    """Set up Cosmos DB database and containers"""
    print("📦 Setting up Cosmos DB...")
    
    # Initialize Cosmos client
    cosmos_client = CosmosClient(os.environ['COSMOS_ENDPOINT'], os.environ['COSMOS_KEY'])
    
    # Create database
    database_name = "FactoryOpsDB"
    try:
        database = cosmos_client.create_database_if_not_exists(id=database_name)
        print(f"✅ Database '{database_name}' ready")
    except Exception as e:
        print(f"❌ Error creating database: {e}")
        return None, None
    
    # Container definitions with partition keys and optional TTL
    containers_config = {
        'Machines': {'partition_key': '/type'},
        'Thresholds': {'partition_key': '/machineType'},
        'Telemetry': {'partition_key': '/machineId', 'ttl': 2592000},  # 30 days TTL
        'KnowledgeBase': {'partition_key': '/machineType'},
        'PartsInventory': {'partition_key': '/category'},
        'Technicians': {'partition_key': '/department'},
        'WorkOrders': {'partition_key': '/status'}
    }
    
    container_clients = {}
    for container_name, config in containers_config.items():
        try:
            container = database.create_container_if_not_exists(
                id=container_name,
                partition_key=PartitionKey(path=config['partition_key']),
                default_ttl=config.get('ttl', None)
            )
            container_clients[container_name] = container
            print(f"✅ Container '{container_name}' ready")
        except Exception as e:
            print(f"❌ Error creating container {container_name}: {e}")
    
    return database, container_clients

def seed_cosmos_data(container_clients):
    """Seed data into Cosmos DB containers"""
    print("📦 Seeding Cosmos DB data...")
    
    # Data file mappings
    data_mappings = {
        'Machines': 'data/machines.json',
        'Thresholds': 'data/thresholds.json',
        'Telemetry': 'data/telemetry-samples.json',
        'KnowledgeBase': 'data/knowledge-base.json',
        'PartsInventory': 'data/parts-inventory.json',
        'Technicians': 'data/technicians.json',
        'WorkOrders': 'data/work-orders.json'
    }
    
    for container_name, file_path in data_mappings.items():
        if container_name in container_clients:
            data = load_json_data(file_path)
            if data:
                container = container_clients[container_name]
                success_count = 0
                for item in data:
                    try:
                        # Ensure document has an id
                        if 'id' not in item:
                            print(f"⚠️ Item in {container_name} missing 'id' field")
                            continue
                        container.create_item(body=item)
                        success_count += 1
                    except Exception as e:
                        if "Conflict" not in str(e):  # Ignore conflicts (already exists)
                            print(f"⚠️ Error inserting item into {container_name}: {e}")
                print(f"✅ Imported {success_count} items into {container_name}")

def main():
    """Main function to orchestrate the data seeding"""
    # Check required environment variables
    required_vars = ['COSMOS_ENDPOINT', 'COSMOS_KEY']
    missing_vars = [var for var in required_vars if not os.environ.get(var)]
    
    if missing_vars:
        print(f"❌ Missing environment variables: {', '.join(missing_vars)}")
        return
    
    # Set up Cosmos DB
    database, container_clients = setup_cosmos_db()
    if container_clients:
        seed_cosmos_data(container_clients)
    
    print("✅ Data seeding completed successfully!")

if __name__ == "__main__":
    main()
EOF

# Run the Python script
echo "🐍 Running data seeding script..."
python3 seed_data.py

# Clean up
rm seed_data.py

echo "✅ Seeding complete!"
