using Azure.AI.Projects;
using Azure.Identity;
using SharedModels;
using Newtonsoft.Json;
using System.Text;
using Azure.AI.Agents.Persistent;

namespace PartsOrderingAgent.Services
{
    public class PartsOrderingAgentService
    {
        private readonly string _projectEndpoint;
        private readonly string _agentId;

        public PartsOrderingAgentService(string projectEndpoint, string agentId)
        {
            _projectEndpoint = projectEndpoint;
            _agentId = agentId;
        }

        /// <summary>
        /// Generate optimized parts order using AI
        /// Note: Persistent agents inherently support memory through thread-based conversations
        /// </summary>
        public async Task<PartsOrder> GeneratePartsOrderAsync(
            WorkOrder workOrder,
            List<InventoryItem> inventory,
            List<Supplier> suppliers)
        {
            // Build context for the AI agent
            var context = BuildOrderingContext(workOrder, inventory, suppliers);

            // Use AIProjectClient for running the agent
            var projectClient = new AIProjectClient(new Uri(_projectEndpoint), new DefaultAzureCredential());
            var persistentClient = new PersistentAgentsClient(_projectEndpoint, new DefaultAzureCredential());
            
            // Get the agent
            var agentResponse = await persistentClient.Administration.GetAgentAsync(_agentId);
            
            // For now, use a simple run without explicit thread management
            // In production, you would create and reuse threads for conversation memory
            
            // TODO: Implement actual agent invocation with Azure.AI.Agents.Persistent SDK
            // For now using a placeholder response that selects the best supplier
            var selectedSupplier = suppliers
                .Where(s => s.Reliability == "High")
                .OrderBy(s => s.LeadTimeDays)
                .FirstOrDefault() ?? suppliers.First();

            var orderItems = workOrder.RequiredParts
                .Where(part => !part.IsAvailable)
                .Select(part => new OrderItem
                {
                    PartNumber = part.PartNumber,
                    PartName = part.PartName,
                    Quantity = part.Quantity,
                    UnitCost = 100.00m, // Placeholder
                    TotalCost = 100.00m * part.Quantity
                }).ToList();

            var order = new PartsOrder
            {
                Id = $"PO-{Guid.NewGuid().ToString().Substring(0, 8)}",
                WorkOrderId = workOrder.Id,
                OrderItems = orderItems,
                SupplierId = selectedSupplier.Id,
                SupplierName = selectedSupplier.Name,
                TotalCost = orderItems.Sum(i => i.TotalCost),
                ExpectedDeliveryDate = DateTime.UtcNow.AddDays(selectedSupplier.LeadTimeDays),
                OrderStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            return order;
        }

        private string BuildOrderingContext(
            WorkOrder workOrder,
            List<InventoryItem> inventory,
            List<Supplier> suppliers)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# Parts Ordering Analysis Request");
            sb.AppendLine();
            sb.AppendLine("## Work Order Information");
            sb.AppendLine($"- Work Order ID: {workOrder.Id}");
            sb.AppendLine($"- Machine ID: {workOrder.MachineId}");
            sb.AppendLine($"- Fault Type: {workOrder.FaultType}");
            sb.AppendLine($"- Priority: {workOrder.Priority}");
            sb.AppendLine();

            sb.AppendLine("## Required Parts");
            foreach (var part in workOrder.RequiredParts)
            {
                sb.AppendLine($"- **{part.PartName}** (Part#: {part.PartNumber})");
                sb.AppendLine($"  * Quantity needed: {part.Quantity}");
                sb.AppendLine($"  * Available in stock: {(part.IsAvailable ? "YES" : "NO")}");
            }
            sb.AppendLine();

            sb.AppendLine("## Current Inventory Status");
            if (inventory.Any())
            {
                foreach (var item in inventory)
                {
                    var needsOrder = item.CurrentStock <= item.ReorderPoint;
                    sb.AppendLine($"- **{item.PartName}** (Part#: {item.PartNumber})");
                    sb.AppendLine($"  * Current Stock: {item.CurrentStock}");
                    sb.AppendLine($"  * Minimum Stock: {item.MinStock}");
                    sb.AppendLine($"  * Reorder Point: {item.ReorderPoint}");
                    sb.AppendLine($"  * Status: {(needsOrder ? "⚠️  NEEDS ORDERING" : "✓ Adequate")}");
                    sb.AppendLine($"  * Location: {item.Location}");
                }
            }
            else
            {
                sb.AppendLine("⚠️  No inventory records found for required parts.");
            }
            sb.AppendLine();

            sb.AppendLine("## Available Suppliers");
            if (suppliers.Any())
            {
                foreach (var supplier in suppliers)
                {
                    sb.AppendLine($"- **{supplier.Name}** (ID: {supplier.Id})");
                    sb.AppendLine($"  * Lead Time: {supplier.LeadTimeDays} days");
                    sb.AppendLine($"  * Reliability: {supplier.Reliability}");
                    sb.AppendLine($"  * Contact: {supplier.ContactEmail}");
                    sb.AppendLine($"  * Parts Available: {string.Join(", ", supplier.Parts.Take(5))}{(supplier.Parts.Count > 5 ? "..." : "")}");
                }
            }
            else
            {
                sb.AppendLine("⚠️  No suppliers found for required parts!");
            }
            sb.AppendLine();

            sb.AppendLine("## Analysis Required");
            sb.AppendLine("Based on the above information, please:");
            sb.AppendLine("1. Determine which parts need to be ordered");
            sb.AppendLine("2. Select the optimal supplier considering:");
            sb.AppendLine("   - Reliability rating (prefer High > Medium > Low)");
            sb.AppendLine("   - Lead time (prefer shorter)");
            sb.AppendLine("   - Part availability");
            sb.AppendLine("3. Calculate:");
            sb.AppendLine("   - Expected delivery date");
            sb.AppendLine("   - Total order cost");
            sb.AppendLine("4. Assign order urgency (CRITICAL if priority=CRITICAL, HIGH if priority=HIGH, otherwise NORMAL)");
            sb.AppendLine();
            sb.AppendLine("Please respond in JSON format:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"supplierId\": \"<selected supplier ID>\",");
            sb.AppendLine("  \"supplierName\": \"<supplier name>\",");
            sb.AppendLine("  \"orderItems\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"partNumber\": \"<part number>\",");
            sb.AppendLine("      \"partName\": \"<part name>\",");
            sb.AppendLine("      \"quantity\": <number>,");
            sb.AppendLine("      \"unitCost\": <decimal>,");
            sb.AppendLine("      \"totalCost\": <decimal>");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"totalCost\": <decimal>,");
            sb.AppendLine("  \"expectedDeliveryDate\": \"<ISO datetime>\",");
            sb.AppendLine("  \"reasoning\": \"<explanation of supplier selection and order decisions>\"");
            sb.AppendLine("}");
            sb.AppendLine("```");

            return sb.ToString();
        }
    }
}
