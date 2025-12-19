using Azure.AI.Projects;
using Azure.Identity;
using SharedModels;
using Newtonsoft.Json;
using System.Text;
using Azure.AI.Agents.Persistent;

namespace PredictiveMaintenanceAgent.Services
{
    public class PredictiveMaintenanceAgentService
    {
        private readonly string _projectEndpoint;
        private readonly string _agentId;

        public PredictiveMaintenanceAgentService(string projectEndpoint, string agentId)
        {
            _projectEndpoint = projectEndpoint;
            _agentId = agentId;
        }

        /// <summary>
        /// Predict optimal maintenance schedule using AI
        /// Note: Persistent agents inherently support memory through thread-based conversations
        /// </summary>
        public async Task<MaintenanceSchedule> PredictMaintenanceScheduleAsync(
            WorkOrder workOrder,
            List<MaintenanceHistory> history,
            List<MaintenanceWindow> availableWindows)
        {
            // Build context for the AI agent
            var context = BuildPredictiveContext(workOrder, history, availableWindows);

            // Use AIProjectClient for running the agent
            var projectClient = new AIProjectClient(new Uri(_projectEndpoint), new DefaultAzureCredential());
            var persistentClient = new PersistentAgentsClient(_projectEndpoint, new DefaultAzureCredential());
            
            //Get the agent
            var agentResponse = await persistentClient.Administration.GetAgentAsync(_agentId);
            
            // For now, use a simple run without explicit thread management
            // In production, you would create and reuse threads for conversation memory
            // Example: var thread = await persistentClient.CreateThreadAsync();
            
            // Run the agent with the query - note this is a simplified version
            // Full memory support requires thread creation and management
            var response = new StringBuilder();
            response.Append(context); // Placeholder - in real implementation, call the agent via API
            
            // For now, return a mock response structure
            // TODO: Implement actual agent invocation with Azure.AI.Agents.Persistent SDK
            // For now using a placeholder response
            var mockScheduleJson = @"{
                ""scheduledDate"": """ + DateTime.UtcNow.AddDays(1).ToString("o") + @""",
                ""maintenanceWindow"": {
                    ""id"": ""WIN-001"",
                    ""startTime"": """ + DateTime.UtcNow.AddDays(1).ToString("o") + @""",
                    ""endTime"": """ + DateTime.UtcNow.AddDays(1).AddHours(4).ToString("o") + @""",
                    ""productionImpact"": ""Low"",
                    ""isAvailable"": true
                },
                ""riskScore"": 75,
                ""predictedFailureProbability"": 0.65,
                ""recommendedAction"": ""URGENT"",
                ""reasoning"": ""Based on historical data, maintenance should be scheduled soon.""
            }";

            // Parse the response into a MaintenanceSchedule object
            var schedule = JsonConvert.DeserializeObject<MaintenanceSchedule>(mockScheduleJson);

            if (schedule == null)
            {
                throw new Exception("Failed to parse maintenance schedule from agent response");
            }

            // Ensure the schedule has required IDs
            schedule.Id = Guid.NewGuid().ToString();
            schedule.WorkOrderId = workOrder.Id;
            schedule.MachineId = workOrder.MachineId;
            schedule.CreatedAt = DateTime.UtcNow;

            return schedule;
        }

        private string BuildPredictiveContext(
            WorkOrder workOrder,
            List<MaintenanceHistory> history,
            List<MaintenanceWindow> availableWindows)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# Predictive Maintenance Analysis Request");
            sb.AppendLine();
            sb.AppendLine("## Work Order Information");
            sb.AppendLine($"- Work Order ID: {workOrder.Id}");
            sb.AppendLine($"- Machine ID: {workOrder.MachineId}");
            sb.AppendLine($"- Fault Type: {workOrder.FaultType}");
            sb.AppendLine($"- Priority: {workOrder.Priority}");
            sb.AppendLine($"- Estimated Duration: {workOrder.EstimatedDurationMinutes} minutes");
            sb.AppendLine();

            sb.AppendLine("## Historical Maintenance Data");
            if (history.Any())
            {
                sb.AppendLine($"Total maintenance events: {history.Count}");
                sb.AppendLine();
                
                var relevantHistory = history.Where(h => h.FaultType == workOrder.FaultType).ToList();
                if (relevantHistory.Any())
                {
                    sb.AppendLine($"**Similar fault type ({workOrder.FaultType}):**");
                    sb.AppendLine($"- Occurrences: {relevantHistory.Count}");
                    sb.AppendLine($"- Average downtime: {relevantHistory.Average(h => h.DowntimeMinutes):F0} minutes");
                    sb.AppendLine($"- Average cost: ${relevantHistory.Average(h => h.Cost):F2}");
                    
                    if (relevantHistory.Count >= 2)
                    {
                        var dates = relevantHistory.Select(h => h.OccurrenceDate).OrderBy(d => d).ToList();
                        var intervals = new List<double>();
                        for (int i = 1; i < dates.Count; i++)
                        {
                            intervals.Add((dates[i] - dates[i-1]).TotalDays);
                        }
                        var avgInterval = intervals.Average();
                        sb.AppendLine($"- Mean Time Between Failures (MTBF): {avgInterval:F0} days");
                        
                        var lastOccurrence = relevantHistory.Max(h => h.OccurrenceDate);
                        var daysSinceLastFailure = (DateTime.UtcNow - lastOccurrence).TotalDays;
                        sb.AppendLine($"- Days since last occurrence: {daysSinceLastFailure:F0}");
                        sb.AppendLine($"- Failure cycle progress: {(daysSinceLastFailure / avgInterval * 100):F1}%");
                    }
                }
                else
                {
                    sb.AppendLine($"**No previous occurrences of {workOrder.FaultType} fault type.**");
                }
                
                sb.AppendLine();
                sb.AppendLine("**Recent maintenance events (all types):**");
                foreach (var record in history.Take(5))
                {
                    sb.AppendLine($"- {record.OccurrenceDate:yyyy-MM-dd}: {record.FaultType} ({record.DowntimeMinutes}min, ${record.Cost})");
                }
            }
            else
            {
                sb.AppendLine("⚠️  No historical maintenance data available for this machine.");
                sb.AppendLine("Risk assessment will be based on fault type and priority only.");
            }
            sb.AppendLine();

            sb.AppendLine("## Available Maintenance Windows (Next 14 Days)");
            if (availableWindows.Any())
            {
                foreach (var window in availableWindows.Take(10))
                {
                    var duration = (window.EndTime - window.StartTime).TotalHours;
                    sb.AppendLine($"- **{window.StartTime:yyyy-MM-dd HH:mm} to {window.EndTime:HH:mm}** ({duration:F1}h)");
                    sb.AppendLine($"  * Production Impact: {window.ProductionImpact}");
                    sb.AppendLine($"  * Window ID: {window.Id}");
                }
            }
            else
            {
                sb.AppendLine("⚠️  No maintenance windows available in the next 14 days!");
                sb.AppendLine("You may need to recommend urgent scheduling outside normal windows.");
            }
            sb.AppendLine();

            sb.AppendLine("## Analysis Required");
            sb.AppendLine("Based on the above information, please:");
            sb.AppendLine("1. Calculate a risk score (0-100) considering:");
            sb.AppendLine("   - Work order priority (CRITICAL=100, HIGH=75, MEDIUM=50, LOW=25 base)");
            sb.AppendLine("   - Time since last similar failure vs MTBF");
            sb.AppendLine("   - Historical downtime and cost impact");
            sb.AppendLine();
            sb.AppendLine("2. Estimate failure probability (0.0-1.0) based on:");
            sb.AppendLine("   - Fault type history");
            sb.AppendLine("   - Cycle progress toward MTBF");
            sb.AppendLine("   - Machine age and usage patterns");
            sb.AppendLine();
            sb.AppendLine("3. Select the optimal maintenance window considering:");
            sb.AppendLine("   - Urgency from risk score (>80=IMMEDIATE, >50=URGENT, <=50=SCHEDULED)");
            sb.AppendLine("   - Production impact (prefer Low, then Medium, then High)");
            sb.AppendLine("   - Window duration vs estimated maintenance time");
            sb.AppendLine();
            sb.AppendLine("4. Recommend action: IMMEDIATE, URGENT, or SCHEDULED");
            sb.AppendLine();
            sb.AppendLine("5. Provide detailed reasoning for your recommendations");
            sb.AppendLine();
            sb.AppendLine("Please respond in JSON format:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"scheduledDate\": \"<ISO datetime of selected window start>\",");
            sb.AppendLine("  \"maintenanceWindow\": {");
            sb.AppendLine("    \"id\": \"<selected window ID>\",");
            sb.AppendLine("    \"startTime\": \"<ISO datetime>\",");
            sb.AppendLine("    \"endTime\": \"<ISO datetime>\",");
            sb.AppendLine("    \"productionImpact\": \"<Low|Medium|High>\",");
            sb.AppendLine("    \"isAvailable\": true");
            sb.AppendLine("  },");
            sb.AppendLine("  \"riskScore\": <number 0-100>,");
            sb.AppendLine("  \"predictedFailureProbability\": <number 0.0-1.0>,");
            sb.AppendLine("  \"recommendedAction\": \"<IMMEDIATE|URGENT|SCHEDULED>\",");
            sb.AppendLine("  \"reasoning\": \"<detailed explanation of your analysis and recommendations>\"");
            sb.AppendLine("}");
            sb.AppendLine("```");

            return sb.ToString();
        }

        private string ExtractJsonFromResponse(string response)
        {
            // Extract JSON from markdown code blocks
            var jsonStart = response.IndexOf("```json");
            if (jsonStart >= 0)
            {
                jsonStart = response.IndexOf('\n', jsonStart) + 1;
                var jsonEnd = response.IndexOf("```", jsonStart);
                return response.Substring(jsonStart, jsonEnd - jsonStart).Trim();
            }

            // Try to find JSON object directly
            jsonStart = response.IndexOf('{');
            if (jsonStart >= 0)
            {
                var jsonEnd = response.LastIndexOf('}');
                return response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            throw new Exception("Could not extract JSON from agent response");
        }
    }
}
