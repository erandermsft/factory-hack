using Microsoft.Extensions.Configuration;
using PredictiveMaintenanceAgent.Services;
using SharedModels;

namespace PredictiveMaintenanceAgent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Predictive Maintenance Agent ===\n");

            // Load configuration
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var cosmosEndpoint = config["COSMOS_ENDPOINT"];
            var cosmosKey = config["COSMOS_KEY"];
            var databaseName = config["COSMOS_DATABASE_NAME"];
            var aiConnectionString = config["AI_FOUNDRY_CONNECTION_STRING"];
            var predMaintenanceAgentId = config["PRED_MAINTENANCE_AGENT_ID"];

            // Validate environment variables
            if (string.IsNullOrEmpty(cosmosEndpoint) || string.IsNullOrEmpty(cosmosKey) || 
                string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(aiConnectionString) || 
                string.IsNullOrEmpty(predMaintenanceAgentId))
            {
                Console.WriteLine("Error: Missing required environment variables.");
                Console.WriteLine("Required: COSMOS_ENDPOINT, COSMOS_KEY, COSMOS_DATABASE_NAME, AI_FOUNDRY_CONNECTION_STRING, PRED_MAINTENANCE_AGENT_ID");
                Console.WriteLine("\nRun CreateAgent.cs first to create the agent and get the PRED_MAINTENANCE_AGENT_ID");
                return;
            }

            // Initialize services
            var cosmosService = new CosmosDbService(cosmosEndpoint, cosmosKey, databaseName);
            var agentService = new PredictiveMaintenanceAgentService(aiConnectionString, predMaintenanceAgentId);

            // Get work order ID from command line or use default
            Console.WriteLine("1. Retrieving work order...");
            var workOrderId = args.Length > 0 ? args[0] : "WO-001"; // Default for testing
            WorkOrder workOrder;
            
            try
            {
                workOrder = await cosmosService.GetWorkOrderAsync(workOrderId);
                Console.WriteLine($"   ✓ Work Order: {workOrder.Id}");
                Console.WriteLine($"   Machine: {workOrder.MachineId}");
                Console.WriteLine($"   Fault: {workOrder.FaultType}");
                Console.WriteLine($"   Priority: {workOrder.Priority}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ✗ Error: {ex.Message}");
                return;
            }

            // Get historical maintenance data
            Console.WriteLine("2. Analyzing historical maintenance data...");
            var history = await cosmosService.GetMaintenanceHistoryAsync(workOrder.MachineId);
            Console.WriteLine($"   ✓ Found {history.Count} historical maintenance records\n");

            // Get available maintenance windows
            Console.WriteLine("3. Checking available maintenance windows...");
            var windows = await cosmosService.GetAvailableMaintenanceWindowsAsync(14);
            Console.WriteLine($"   ✓ Found {windows.Count} available windows in next 14 days\n");

            // Run predictive analysis
            Console.WriteLine("4. Running AI predictive analysis...");
            try
            {
                var schedule = await agentService.PredictMaintenanceScheduleAsync(workOrder, history, windows);
                Console.WriteLine($"   ✓ Analysis complete!\n");

                // Display results
                Console.WriteLine("=== Predictive Maintenance Schedule ===");
                Console.WriteLine($"Schedule ID: {schedule.Id}");
                Console.WriteLine($"Machine: {schedule.MachineId}");
                Console.WriteLine($"Scheduled Date: {schedule.ScheduledDate:yyyy-MM-dd HH:mm}");
                Console.WriteLine($"Window: {schedule.MaintenanceWindow.StartTime:HH:mm} - {schedule.MaintenanceWindow.EndTime:HH:mm}");
                Console.WriteLine($"Production Impact: {schedule.MaintenanceWindow.ProductionImpact}");
                Console.WriteLine($"Risk Score: {schedule.RiskScore}/100");
                Console.WriteLine($"Failure Probability: {schedule.PredictedFailureProbability * 100:F1}%");
                Console.WriteLine($"Recommended Action: {schedule.RecommendedAction}");
                Console.WriteLine($"\nReasoning:");
                Console.WriteLine($"{schedule.Reasoning}");
                Console.WriteLine();

                // Save schedule to Cosmos DB
                Console.WriteLine("5. Saving maintenance schedule...");
                await cosmosService.SaveMaintenanceScheduleAsync(schedule);
                Console.WriteLine($"   ✓ Schedule saved to Cosmos DB\n");

                // Update work order status
                Console.WriteLine("6. Updating work order status...");
                await cosmosService.UpdateWorkOrderStatusAsync(workOrder.Id, "Scheduled");
                Console.WriteLine($"   ✓ Work order status updated to 'Scheduled'\n");

                Console.WriteLine("✓ Predictive Maintenance Agent completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ✗ Error during predictive analysis: {ex.Message}");
                Console.WriteLine($"\nStack trace: {ex.StackTrace}");
            }
        }
    }
}
