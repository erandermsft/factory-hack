using Azure.AI.Agents.Persistent;
using Azure.Identity;

namespace PredictiveMaintenanceAgent
{
    /// <summary>
    /// Creates the Predictive Maintenance Agent in Microsoft Foundry
    /// Run this once to set up the agent, then use the returned Agent ID in your .env file
    /// </summary>
    class CreateAgent
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Creating Predictive Maintenance Agent in Microsoft Foundry ===\n");

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
                var agentName = "PredictiveMaintenanceAgent";
                var deploymentName = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME") ?? "gpt-4o";
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

                Console.WriteLine("Creating persistent agent in Foundry with memory enabled...");
                
                // Create a persistent agent with memory capabilities
                // Memory allows the agent to maintain context across conversations
                var agentOptions = new Azure.AI.Agents.Persistent.AgentCreationOptions(deploymentName)
                {
                    Name = agentName,
                    Instructions = instructions,
                    Temperature = 0.3f,
                    // Enable memory features for conversation persistence
                    MemoryEnabled = true
                };
                
                var persistentAgent = await persistentAgentsClient.Administration.CreateAgentAsync(agentOptions);

                Console.WriteLine($"\n✓ Agent created successfully with memory enabled!");
                Console.WriteLine($"\nAgent Name: {agentName}");
                Console.WriteLine($"Agent ID: {persistentAgent.Value.Id}");
                Console.WriteLine($"Memory: Enabled (maintains context across conversations)");
                Console.WriteLine($"\nAdd this to your environment variables:");
                Console.WriteLine($"export PRED_MAINTENANCE_AGENT_ID={persistentAgent.Value.Id}");
                Console.WriteLine($"\nOr add to your .env file:");
                Console.WriteLine($"PRED_MAINTENANCE_AGENT_ID={persistentAgent.Value.Id}");
                Console.WriteLine($"\nThe agent should now be visible in the Microsoft Foundry portal!");
                Console.WriteLine($"\nWith memory enabled, the agent will:");
                Console.WriteLine($"- Remember previous maintenance analyses");
                Console.WriteLine($"- Track machine-specific patterns across sessions");
                Console.WriteLine($"- Maintain conversation context for follow-up queries");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error creating agent: {ex.Message}");
                Console.WriteLine($"\nStack trace: {ex.StackTrace}");
            }
        }
    }
}
