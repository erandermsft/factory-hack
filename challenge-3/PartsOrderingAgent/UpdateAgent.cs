using Azure.AI.Agents.Persistent;
using Azure.Identity;

namespace PartsOrderingAgent
{
    /// <summary>
    /// Updates the Parts Ordering Agent deployment in Microsoft Foundry
    /// </summary>
    class UpdateAgent
    {
        // Uncomment this to run UpdateAgent, then comment it back
        // static async Task Main(string[] args)
        static async Task MainUpdate(string[] args)
        {
            Console.WriteLine("=== Updating Parts Ordering Agent ===\n");

            var projectEndpoint = Environment.GetEnvironmentVariable("AI_FOUNDRY_CONNECTION_STRING");
            var agentId = Environment.GetEnvironmentVariable("PARTS_ORDERING_AGENT_ID");
            var deploymentName = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME");

            if (string.IsNullOrEmpty(projectEndpoint) || string.IsNullOrEmpty(agentId))
            {
                Console.WriteLine("Error: Missing required environment variables");
                Console.WriteLine("Required: AI_FOUNDRY_CONNECTION_STRING, PARTS_ORDERING_AGENT_ID");
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
                var instructions = @"You are a parts ordering specialist for industrial tire manufacturing equipment. Your role is to analyze inventory status and optimize parts ordering from suppliers.

When processing parts orders:
1. Review current inventory levels for required parts
2. Check against minimum stock and reorder points
3. Identify suppliers for needed parts considering:
   - Lead time
   - Reliability rating (High/Medium/Low)
   - Cost optimization
4. Create optimized orders by:
   - Grouping parts by supplier when possible
   - Prioritizing reliable suppliers with shorter lead times
   - Calculating expected delivery dates
   - Computing total order costs

Always provide:
- Clear inventory status assessment
- Supplier selection with justification
- Expected delivery date calculation
- Total cost breakdown
- Order urgency level (CRITICAL, HIGH, NORMAL)

Respond in JSON format as specified in the user's request.";

                var newAgent = await persistentAgentsClient.Administration.CreateAgentAsync(
                    model: deploymentName,
                    name: "PartsOrderingAgent",
                    instructions: instructions,
                    temperature: 0.3f
                );

                Console.WriteLine($"\n✓ Agent recreated successfully!");
                Console.WriteLine($"\nNew Agent ID: {newAgent.Value.Id}");
                Console.WriteLine($"Deployment: {deploymentName}");
                Console.WriteLine($"\n⚠️  IMPORTANT: Update your .env file with the new Agent ID:");
                Console.WriteLine($"PARTS_ORDERING_AGENT_ID={newAgent.Value.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error: {ex.Message}");
                Console.WriteLine($"\nStack trace: {ex.StackTrace}");
            }
        }
    }
}
