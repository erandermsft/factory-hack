using Newtonsoft.Json;

namespace SharedModels
{
    /// <summary>
    /// Work order from the Repair Planner Agent
    /// </summary>
    public class WorkOrder
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("machineId")]
        public string MachineId { get; set; } = string.Empty;
        
        [JsonProperty("faultType")]
        public string FaultType { get; set; } = string.Empty;
        
        [JsonProperty("priority")]
        public string Priority { get; set; } = string.Empty;
        
        [JsonProperty("assignedTechnician")]
        public string AssignedTechnician { get; set; } = string.Empty;
        
        [JsonProperty("requiredParts")]
        public List<RequiredPart> RequiredParts { get; set; } = new();
        
        [JsonProperty("estimatedDuration")]
        public int EstimatedDurationMinutes { get; set; }
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
        
        [JsonProperty("status")]
        public string Status { get; set; } = "Created"; // "Created", "Scheduled", "PartsOrdered", "Ready", "InProgress", "Completed"
    }

    /// <summary>
    /// Part required for maintenance
    /// </summary>
    public class RequiredPart
    {
        [JsonProperty("partNumber")]
        public string PartNumber { get; set; } = string.Empty;
        
        [JsonProperty("partName")]
        public string PartName { get; set; } = string.Empty;
        
        [JsonProperty("quantity")]
        public int Quantity { get; set; }
        
        [JsonProperty("isAvailable")]
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Predictive maintenance schedule output
    /// </summary>
    public class MaintenanceSchedule
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("workOrderId")]
        public string WorkOrderId { get; set; } = string.Empty;
        
        [JsonProperty("machineId")]
        public string MachineId { get; set; } = string.Empty;
        
        [JsonProperty("scheduledDate")]
        public DateTime ScheduledDate { get; set; }
        
        [JsonProperty("maintenanceWindow")]
        public MaintenanceWindow MaintenanceWindow { get; set; } = new();
        
        [JsonProperty("riskScore")]
        public double RiskScore { get; set; }
        
        [JsonProperty("predictedFailureProbability")]
        public double PredictedFailureProbability { get; set; }
        
        [JsonProperty("recommendedAction")]
        public string RecommendedAction { get; set; } = string.Empty;
        
        [JsonProperty("reasoning")]
        public string Reasoning { get; set; } = string.Empty;
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Available maintenance window from MES
    /// </summary>
    public class MaintenanceWindow
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }
        
        [JsonProperty("endTime")]
        public DateTime EndTime { get; set; }
        
        [JsonProperty("productionImpact")]
        public string ProductionImpact { get; set; } = string.Empty; // "Low", "Medium", "High"
        
        [JsonProperty("isAvailable")]
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Historical maintenance record for prediction
    /// </summary>
    public class MaintenanceHistory
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("machineId")]
        public string MachineId { get; set; } = string.Empty;
        
        [JsonProperty("faultType")]
        public string FaultType { get; set; } = string.Empty;
        
        [JsonProperty("occurrenceDate")]
        public DateTime OccurrenceDate { get; set; }
        
        [JsonProperty("resolutionDate")]
        public DateTime ResolutionDate { get; set; }
        
        [JsonProperty("downtime")]
        public int DowntimeMinutes { get; set; }
        
        [JsonProperty("cost")]
        public decimal Cost { get; set; }
    }

    /// <summary>
    /// Parts order for SCM system
    /// </summary>
    public class PartsOrder
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("workOrderId")]
        public string WorkOrderId { get; set; } = string.Empty;
        
        [JsonProperty("orderItems")]
        public List<OrderItem> OrderItems { get; set; } = new();
        
        [JsonProperty("supplierId")]
        public string SupplierId { get; set; } = string.Empty;
        
        [JsonProperty("supplierName")]
        public string SupplierName { get; set; } = string.Empty;
        
        [JsonProperty("totalCost")]
        public decimal TotalCost { get; set; }
        
        [JsonProperty("expectedDeliveryDate")]
        public DateTime ExpectedDeliveryDate { get; set; }
        
        [JsonProperty("orderStatus")]
        public string OrderStatus { get; set; } = "Pending"; // "Pending", "Ordered", "Shipped", "Delivered"
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Individual item in a parts order
    /// </summary>
    public class OrderItem
    {
        [JsonProperty("partNumber")]
        public string PartNumber { get; set; } = string.Empty;
        
        [JsonProperty("partName")]
        public string PartName { get; set; } = string.Empty;
        
        [JsonProperty("quantity")]
        public int Quantity { get; set; }
        
        [JsonProperty("unitCost")]
        public decimal UnitCost { get; set; }
        
        [JsonProperty("totalCost")]
        public decimal TotalCost { get; set; }
    }

    /// <summary>
    /// Inventory item from WMS
    /// </summary>
    public class InventoryItem
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("partNumber")]
        public string PartNumber { get; set; } = string.Empty;
        
        [JsonProperty("partName")]
        public string PartName { get; set; } = string.Empty;
        
        [JsonProperty("currentStock")]
        public int CurrentStock { get; set; }
        
        [JsonProperty("minStock")]
        public int MinStock { get; set; }
        
        [JsonProperty("reorderPoint")]
        public int ReorderPoint { get; set; }
        
        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;
    }

    /// <summary>
    /// Supplier information from SCM
    /// </summary>
    public class Supplier
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonProperty("parts")]
        public List<string> Parts { get; set; } = new();
        
        [JsonProperty("leadTimeDays")]
        public int LeadTimeDays { get; set; }
        
        [JsonProperty("reliability")]
        public string Reliability { get; set; } = string.Empty; // "High", "Medium", "Low"
        
        [JsonProperty("contactEmail")]
        public string ContactEmail { get; set; } = string.Empty;
    }
}
