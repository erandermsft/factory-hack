using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RepairPlannerAgent.Models;
using RepairPlannerAgent.Services;

namespace RepairPlannerAgent;

public static class Program
{
    public static async Task<int> Main()
    {
        var cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT");
        var cosmosKey = Environment.GetEnvironmentVariable("COSMOS_KEY");
        var cosmosDatabaseName = Environment.GetEnvironmentVariable("COSMOS_DATABASE_NAME");

        var aiEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_CHAT_ENDPOINT");
        var aiKey = Environment.GetEnvironmentVariable("AZURE_AI_CHAT_KEY");
        var aiModelDeployment = Environment.GetEnvironmentVariable("AZURE_AI_CHAT_MODEL_DEPLOYMENT_NAME");

        if (string.IsNullOrWhiteSpace(cosmosEndpoint) ||
            string.IsNullOrWhiteSpace(cosmosKey) ||
            string.IsNullOrWhiteSpace(cosmosDatabaseName) ||
            string.IsNullOrWhiteSpace(aiEndpoint) ||
            string.IsNullOrWhiteSpace(aiKey) ||
            string.IsNullOrWhiteSpace(aiModelDeployment))
        {
            Console.Error.WriteLine("Missing required environment variables.");
            Console.Error.WriteLine("Required:");
            Console.Error.WriteLine("  COSMOS_ENDPOINT");
            Console.Error.WriteLine("  COSMOS_KEY");
            Console.Error.WriteLine("  COSMOS_DATABASE_NAME");
            Console.Error.WriteLine("  AZURE_AI_CHAT_ENDPOINT");
            Console.Error.WriteLine("  AZURE_AI_CHAT_KEY");
            Console.Error.WriteLine("  AZURE_AI_MODEL_DEPLOYMENT_NAME");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example:");
            Console.Error.WriteLine("  export COSMOS_ENDPOINT='https://<account>.documents.azure.com:443/'");
            Console.Error.WriteLine("  export COSMOS_KEY='<key>'");
            Console.Error.WriteLine("  export COSMOS_DATABASE_NAME='factory-db'");
            Console.Error.WriteLine("  export AZURE_AI_CHAT_ENDPOINT='https://<resource>.openai.azure.com/'");
            Console.Error.WriteLine("  export AZURE_AI_CHAT_KEY='<key>'");
            Console.Error.WriteLine("  export AZURE_AI_MODEL_DEPLOYMENT_NAME='gpt-4.1-mini'");
            return 2;
        }

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }));

        var cosmosLogger = loggerFactory.CreateLogger<CosmosDbService>();
        var aiLogger = loggerFactory.CreateLogger<AIFoundryService>();
        var plannerLogger = loggerFactory.CreateLogger<RepairPlanner>();

        using var cosmosService = new CosmosDbService(
            cosmosEndpoint,
            cosmosKey,
            cosmosDatabaseName,
            cosmosLogger);

        var aiService = new AIFoundryService(
            aiEndpoint,
            aiKey,
            aiModelDeployment,
            aiLogger);

        var planner = new RepairPlanner(cosmosService, aiService, plannerLogger);

        // Sample input fault.
        var fault = new DiagnosedFault
        {
            MachineId = "machine-001",
            FaultType = "curing_temperature_excessive",
            RootCause = "Suspected heater band degradation or thermocouple drift.",
            Severity = "high",
            DetectedAt = DateTimeOffset.UtcNow,
            Metadata =
            {
                ["observed_temperature_c"] = 192,
                ["setpoint_temperature_c"] = 170,
                ["zone"] = "mold_heating"
            }
        };

        try
        {
            var workOrder = await planner.PlanRepairAsync(fault);

            Console.WriteLine();
            Console.WriteLine("=== Generated Work Order ===");
            Console.WriteLine(JsonConvert.SerializeObject(workOrder, Formatting.Indented));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Repair planning failed:");
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            // loggerFactory and cosmosService disposed via using.
        }
    }
}
