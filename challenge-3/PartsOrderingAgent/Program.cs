using Microsoft.Extensions.Configuration;
using PartsOrderingAgent.Services;
using SharedModels;

namespace PartsOrderingAgent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Parts Ordering Agent ===\n");

            // Load configuration
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var cosmosEndpoint = config["COSMOS_ENDPOINT"];
            var cosmosKey = config["COSMOS_KEY"];
            var databaseName = config["COSMOS_DATABASE_NAME"];
            var aiConnectionString = config["AI_FOUNDRY_CONNECTION_STRING"];
            var partsOrderingAgentId = config["PARTS_ORDERING_AGENT_ID"];

            // Validate environment variables
            if (string.IsNullOrEmpty(cosmosEndpoint) || string.IsNullOrEmpty(cosmosKey) || 
                string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(aiConnectionString) || 
                string.IsNullOrEmpty(partsOrderingAgentId))
            {
                Console.WriteLine("Error: Missing required environment variables.");
                Console.WriteLine("Required: COSMOS_ENDPOINT, COSMOS_KEY, COSMOS_DATABASE_NAME, AI_FOUNDRY_CONNECTION_STRING, PARTS_ORDERING_AGENT_ID");
                Console.WriteLine("\nRun CreateAgent.cs first to create the agent and get the PARTS_ORDERING_AGENT_ID");
                return;
            }

            // Initialize services
            var cosmosService = new CosmosDbService(cosmosEndpoint, cosmosKey, databaseName);
            var agentService = new PartsOrderingAgentService(aiConnectionString, partsOrderingAgentId);

            // Get work order ID from command line or use default
            Console.WriteLine("1. Retrieving work order...");
            var workOrderId = args.Length > 0 ? args[0] : "WO-001"; // Default for testing
            WorkOrder workOrder;
            
            try
            {
                workOrder = await cosmosService.GetWorkOrderAsync(workOrderId);
                Console.WriteLine($"   ✓ Work Order: {workOrder.Id}");
                Console.WriteLine($"   Machine: {workOrder.MachineId}");
                Console.WriteLine($"   Required Parts: {workOrder.RequiredParts.Count}");
                Console.WriteLine($"   Priority: {workOrder.Priority}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ✗ Error: {ex.Message}");
                return;
            }

            // Check inventory for required parts
            Console.WriteLine("2. Checking inventory status...");
            var partNumbers = workOrder.RequiredParts.Select(p => p.PartNumber).ToList();
            var inventory = await cosmosService.GetInventoryItemsAsync(partNumbers);
            Console.WriteLine($"   ✓ Found {inventory.Count} inventory records\n");

            // Identify parts that need ordering
            var partsNeedingOrder = workOrder.RequiredParts
                .Where(p => !p.IsAvailable)
                .ToList();

            if (!partsNeedingOrder.Any())
            {
                Console.WriteLine("✓ All required parts are available in stock!");
                Console.WriteLine("No parts order needed.\n");
                
                // Update work order status
                Console.WriteLine("3. Updating work order status...");
                await cosmosService.UpdateWorkOrderStatusAsync(workOrder.Id, "Ready");
                Console.WriteLine($"   ✓ Work order status updated to 'Ready'\n");
                
                Console.WriteLine("✓ Parts Ordering Agent completed successfully!");
                return;
            }

            Console.WriteLine($"⚠️  {partsNeedingOrder.Count} part(s) need to be ordered:");
            foreach (var part in partsNeedingOrder)
            {
                Console.WriteLine($"   - {part.PartName} (Qty: {part.Quantity})");
            }
            Console.WriteLine();

            // Get suppliers for needed parts
            Console.WriteLine("3. Finding suppliers...");
            var neededPartNumbers = partsNeedingOrder.Select(p => p.PartNumber).ToList();
            var suppliers = await cosmosService.GetSuppliersForPartsAsync(neededPartNumbers);
            Console.WriteLine($"   ✓ Found {suppliers.Count} potential suppliers\n");

            if (!suppliers.Any())
            {
                Console.WriteLine("✗ No suppliers found for required parts!");
                return;
            }

            // Generate optimized parts order
            Console.WriteLine("4. Running AI parts ordering analysis...");
            try
            {
                var order = await agentService.GeneratePartsOrderAsync(workOrder, inventory, suppliers);
                Console.WriteLine($"   ✓ Parts order generated!\n");

                // Display results
                Console.WriteLine("=== Parts Order ===");
                Console.WriteLine($"Order ID: {order.Id}");
                Console.WriteLine($"Work Order: {order.WorkOrderId}");
                Console.WriteLine($"Supplier: {order.SupplierName} (ID: {order.SupplierId})");
                Console.WriteLine($"Expected Delivery: {order.ExpectedDeliveryDate:yyyy-MM-dd}");
                Console.WriteLine($"Total Cost: ${order.TotalCost:F2}");
                Console.WriteLine($"Status: {order.OrderStatus}");
                Console.WriteLine($"\nOrder Items:");
                foreach (var item in order.OrderItems)
                {
                    Console.WriteLine($"  - {item.PartName} (#{item.PartNumber})");
                    Console.WriteLine($"    Qty: {item.Quantity} @ ${item.UnitCost:F2} = ${item.TotalCost:F2}");
                }
                Console.WriteLine();

                // Save order to Cosmos DB
                Console.WriteLine("5. Saving parts order...");
                await cosmosService.SavePartsOrderAsync(order);
                Console.WriteLine($"   ✓ Order saved to SCM system\n");

                // Update work order status
                Console.WriteLine("6. Updating work order status...");
                await cosmosService.UpdateWorkOrderStatusAsync(workOrder.Id, "PartsOrdered");
                Console.WriteLine($"   ✓ Work order status updated to 'PartsOrdered'\n");

                Console.WriteLine("✓ Parts Ordering Agent completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ✗ Error during parts ordering: {ex.Message}");
                Console.WriteLine($"\nStack trace: {ex.StackTrace}");
            }
        }
    }
}
