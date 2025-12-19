using Azure.AI.Agents.Persistent;
using Azure.Identity;

namespace PartsOrderingAgent
{
    /// <summary>
    /// Creates the Parts Ordering Agent in Microsoft Foundry
    /// Run this once to set up the agent, then use the returned Agent ID in your .env file
    /// </summary>
    class CreateAgent
    {
        // Uncomment this to run CreateAgent, then comment it out again before running Program
        // static async Task Main(string[] args)
        static async Task MainCreate(string[] args)
        {
            Console.WriteLine("=== Creating Parts Ordering Agent in Microsoft Foundry ===\n");

            // Load connection string from environment
            var projectEndpoint = Environment.GetEnvironmentVariable("AI_FOUNDRY_CONNECTION_STRING");
            if (string.IsNullOrEmpty(projectEndpoint))
            {
                Console.WriteLine("Error: AI_FOUNDRY_CONNECTION_STRING environment variable not set");
                Console.WriteLine("\nPlease set the connection string in your environment:");
                Console.WriteLine("export AI_FOUNDRY_CONNECTION_STRING='your-connection-string'");
                return;
            }

            try
            {
                // Create PersistentAgentsClient
                var persistentAgentsClient = new PersistentAgentsClient(
                    projectEndpoint,
                    new DefaultAzureCredential()
                );

                // Define agent configuration
                var agentName = "PartsOrderingAgent";
                var deploymentName = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME") ?? "gpt-4o";
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

                Console.WriteLine("Creating persistent agent in Foundry...");
                
                // Create a persistent agent that will show up in the Foundry portal
                // Persistent agents automatically support memory through threads
                var persistentAgent = await persistentAgentsClient.Administration.CreateAgentAsync(
                    model: deploymentName,
                    name: agentName,
                    instructions: instructions,
                    temperature: 0.3f
                );

                Console.WriteLine($"\n✓ Agent created successfully!");
                Console.WriteLine($"\nAgent Name: {agentName}");
                Console.WriteLine($"Agent ID: {persistentAgent.Value.Id}");
                Console.WriteLine($"\nAdd this to your environment variables:");
                Console.WriteLine($"export PARTS_ORDERING_AGENT_ID={persistentAgent.Value.Id}");
                Console.WriteLine($"\nOr add to your .env file:");
                Console.WriteLine($"PARTS_ORDERING_AGENT_ID={persistentAgent.Value.Id}");
                Console.WriteLine($"\nThe agent should now be visible in the Microsoft Foundry portal!");
                Console.WriteLine($"\nNote: Persistent agents support conversation memory through threads.");
                Console.WriteLine($"Each thread maintains its own context and message history.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error creating agent: {ex.Message}");
                Console.WriteLine($"\nStack trace: {ex.StackTrace}");
            }
        }
    }
}
