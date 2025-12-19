using Azure.AI.Agents.Persistent;
using Azure.Identity;

namespace PredictiveMaintenanceAgent
{
    /// <summary>
    /// Updates the Predictive Maintenance Agent deployment in Microsoft Foundry
    /// </summary>
    class UpdateAgent
    {
        // Uncomment this to run UpdateAgent, then comment it back
        // static async Task Main(string[] args)
        static async Task MainUpdate(string[] args)
        {
            Console.WriteLine("=== Updating Predictive Maintenance Agent ===\n");

            var projectEndpoint = Environment.GetEnvironmentVariable("AI_FOUNDRY_CONNECTION_STRING");
            var agentId = Environment.GetEnvironmentVariable("PRED_MAINTENANCE_AGENT_ID");
            var deploymentName = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME");

            if (string.IsNullOrEmpty(projectEndpoint) || string.IsNullOrEmpty(agentId))
            {
                Console.WriteLine("Error: Missing required environment variables");
                Console.WriteLine("Required: AI_FOUNDRY_CONNECTION_STRING, PRED_MAINTENANCE_AGENT_ID");
                return;
            }

            if (string.IsNullOrEmpty(deploymentName))
            {
                Console.WriteLine("Error: AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME not set");
                Console.WriteLine("Please check your .env file and ensure the deployment name is correct");
                return;
            }

            Console.WriteLine($"Project Endpoint: {projectEndpoint}");
            Console.WriteLine($"Agent ID: {agentId}");
            Console.WriteLine($"Target Deployment: {deploymentName}\n");

            try
            {
                var persistentAgentsClient = new PersistentAgentsClient(
                    projectEndpoint,
                    new DefaultAzureCredential()
                );

                // Get the current agent
                Console.WriteLine("Fetching current agent...");
                var currentAgent = await persistentAgentsClient.Administration.GetAgentAsync(agentId);
                Console.WriteLine($"✓ Found agent: {currentAgent.Value.Name}\n");

                // Delete the old agent
                Console.WriteLine("Deleting old agent...");
                await persistentAgentsClient.Administration.DeleteAgentAsync(agentId);
                Console.WriteLine("✓ Old agent deleted\n");

                // Recreate with correct deployment
                Console.WriteLine($"Creating new agent with deployment '{deploymentName}'...");
                var instructions = @"You are a predictive maintenance expert specializing in industrial tire manufacturing equipment. Your role is to analyze historical maintenance data and recommend optimal maintenance schedules.

When analyzing maintenance needs:
1. Review historical failure patterns for the specific machine
2. Calculate risk scores based on:
   - Time since last maintenance
   - Frequency of similar faults
   - Average downtime costs
   - Current machine criticality
3. Assess failure probability considering:
   - Mean Time Between Failures (MTBF)
   - Current operational hours since last service
   - Fault type severity
4. Recommend maintenance windows by:
   - Prioritizing low production impact periods
   - Considering the urgency (IMMEDIATE if risk > 80, URGENT if risk > 50, otherwise SCHEDULED)
   - Balancing cost optimization with safety

Always provide:
- Quantitative risk score (0-100)
- Failure probability (0-1)
- Specific maintenance window selection with justification
- Clear action recommendation (IMMEDIATE/URGENT/SCHEDULED)
- Detailed reasoning based on data analysis

Respond in JSON format as specified in the user's request.";

                var newAgent = await persistentAgentsClient.Administration.CreateAgentAsync(
                    model: deploymentName,
                    name: "PredictiveMaintenanceAgent",
                    instructions: instructions,
                    temperature: 0.3f
                );

                Console.WriteLine($"\n✓ Agent recreated successfully!");
                Console.WriteLine($"\nNew Agent ID: {newAgent.Value.Id}");
                Console.WriteLine($"Deployment: {deploymentName}");
                Console.WriteLine($"\n⚠️  IMPORTANT: Update your .env file with the new Agent ID:");
                Console.WriteLine($"PRED_MAINTENANCE_AGENT_ID={newAgent.Value.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error: {ex.Message}");
                Console.WriteLine($"\nStack trace: {ex.StackTrace}");
            }
        }
    }
}
