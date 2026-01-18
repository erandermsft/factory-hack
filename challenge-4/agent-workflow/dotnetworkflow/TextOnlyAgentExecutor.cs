using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

namespace FactoryWorkflow;

/// <summary>
/// Custom agent executor that only passes TEXT output between agents in a sequential workflow.
/// This works around the Azure.AI.Projects SDK bug where MCP tool call history in
/// conversation messages causes deserialization errors when passed to subsequent agents.
/// 
/// The executor is Executor{string, string} so outputs can directly chain to next agent's input.
/// Agent step details (tool calls, etc.) are collected via the static StepResults property.
/// </summary>
public sealed class TextOnlyAgentExecutor : Executor<string, string>
{
    private readonly AIAgent _agent;

    // Static collector for step results - ConcurrentQueue preserves FIFO order
    private static readonly ConcurrentQueue<AgentStepResult> _stepResults = new();
    
    public static IEnumerable<AgentStepResult> StepResults => _stepResults.ToArray();

    public static void ClearResults()
    {
        while (_stepResults.TryDequeue(out _)) { }
    }

    public TextOnlyAgentExecutor(AIAgent agent) : base($"TextOnly-{agent.Name}")
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    public string AgentName => _agent.Name;

    public override async ValueTask<string> HandleAsync(string input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var agentStep = new AgentStepResult { AgentName = _agent.Name };

        try
        {
            // Create a clean message with only text input (no MCP tool history)
            var messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, input) };

            // Run the agent
            var response = await _agent.RunAsync(messages, null);

            // Extract text output and tool calls from response
            string? outputText = null;
            if (response.Messages != null)
            {
                foreach (var msg in response.Messages)
                {
                    if (msg.Role == ChatRole.Assistant)
                    {
                        foreach (var content in msg.Contents)
                        {
                            if (content is TextContent tc && !string.IsNullOrWhiteSpace(tc.Text))
                            {
                                outputText = tc.Text;
                                agentStep.TextOutput += tc.Text;
                            }
                            else if (content is FunctionCallContent fcc)
                            {
                                agentStep.ToolCalls.Add(new ToolCallInfo
                                {
                                    ToolName = fcc.Name,
                                    CallId = fcc.CallId,
                                    Arguments = fcc.Arguments?.ToString()
                                });
                            }
                        }
                    }
                    else if (msg.Role == ChatRole.Tool)
                    {
                        foreach (var content in msg.Contents)
                        {
                            if (content is FunctionResultContent frc)
                            {
                                var matchingCall = agentStep.ToolCalls.LastOrDefault(t => t.CallId == frc.CallId);
                                if (matchingCall != null)
                                {
                                    matchingCall.Result = frc.Result?.ToString()?.Substring(0, Math.Min(500, frc.Result?.ToString()?.Length ?? 0));
                                }
                            }
                        }
                    }
                }
            }

            agentStep.FinalMessage = outputText ?? "";
            Console.WriteLine($"Agent {_agent.Name} completed with {agentStep.ToolCalls.Count} tool calls.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Agent {_agent.Name} failed: {ex.Message}");
            agentStep.FinalMessage = $"Error: {ex.Message}";
            agentStep.TextOutput = $"Error: {ex.Message}";
        }

        // Store step result for later collection (ConcurrentQueue preserves order)
        _stepResults.Enqueue(agentStep);

        // Return just the text for the next agent
        return agentStep.FinalMessage ?? "";
    }
}

/// <summary>
/// Represents a tool/function call made by an agent
/// </summary>
public class ToolCallInfo
{
    public string ToolName { get; set; } = string.Empty;
    public string? CallId { get; set; }
    public string? Arguments { get; set; }
    public string? Result { get; set; }
}
