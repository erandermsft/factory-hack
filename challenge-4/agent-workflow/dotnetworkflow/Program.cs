using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.AI.Agents.Persistent;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AzureAI;
using Microsoft.Agents.AI.A2A;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Azure.Monitor.OpenTelemetry.Exporter;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using A2A;

using FactoryWorkflow.RepairPlanner;
using FactoryWorkflow.RepairPlanner.Services;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
// Dev/Codespaces: allow the Vite frontend (different origin) to call this API.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Configuration.AddEnvironmentVariables();

var configuration = builder.Configuration;
const string SourceName = "FactoryWorkflow";
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(
        serviceName: builder.Environment.ApplicationName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString())
    .AddAttributes([
        new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName),
    ]);

Console.WriteLine(builder.Configuration["AZURE_AI_PROJECT_ENDPOINT"]);
// Configuration validation
var projectEndpoint = builder.Configuration["AZURE_AI_PROJECT_ENDPOINT"];
if (string.IsNullOrEmpty(projectEndpoint))
{
    Console.WriteLine("Warning: AZURE_AI_PROJECT_ENDPOINT is not set in configuration.");
}
else
{
    Console.WriteLine($"AZURE_AI_PROJECT_ENDPOINT is set to {projectEndpoint}");
}

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["AZURE_AI_PROJECT_ENDPOINT"];
    if (string.IsNullOrEmpty(endpoint)) throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT is missing");
    return new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());
});

// Register LoggerFactory for A2A agents
builder.Services.AddSingleton<ILoggerFactory>(sp => LoggerFactory.Create(b => b.AddConsole()));

var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];

var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(SourceName, "ChatClient") // Our custom activity source(s)
    .AddSource("Microsoft.Agents.AI*") // Agent Framework telemetry
    .AddSource("AnomalyClassificationAgent", "FaultDiagnosisAgent", "RepairPlannerAgent") // Our agents
    .AddSource("MaintenanceSchedulerAgent", "PartsOrderingAgent") // A2A agents from challenge-3
    .AddAspNetCoreInstrumentation() // Capture incoming HTTP requests
    .AddHttpClientInstrumentation() // Capture HTTP calls to OpenAI
    .AddOtlpExporter();

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    tracerProviderBuilder = tracerProviderBuilder.AddAzureMonitorTraceExporter(options =>
        options.ConnectionString = appInsightsConnectionString);
}
else
{
    Console.WriteLine("Warning: ApplicationInsights:ConnectionString is not set; Azure Monitor trace exporter is disabled.");
}

using var tracerProvider = tracerProviderBuilder.Build();

var app = builder.Build();
app.UseCors();
app.MapPost("/api/analyze_machine", AnalyzeMachine);
app.MapPost("/api/analyze_machine_stream", AnalyzeMachineStream);
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow }));
app.Run();

static async Task<IResult> AnalyzeMachine(
    AnalyzeRequest request,
    AIProjectClient projectClient,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILoggerFactory loggerFactory,
    ILogger<Program> logger)
{
    logger.LogInformation("Starting analysis for machine {MachineId}", request.machine_id);

    try
    {
        AIAgent anomalyClassificationAgent = projectClient.GetAIAgent("AnomalyClassificationAgent");
        AIAgent faultDiagnosisAgent = projectClient.GetAIAgent("FaultDiagnosisAgent");

        Console.WriteLine($"Agent retrieved (name: {faultDiagnosisAgent.Name}, id: {faultDiagnosisAgent.Id})");
        Console.WriteLine($"Agent retrieved (name: {anomalyClassificationAgent.Name}, id: {anomalyClassificationAgent.Id})");
        
        var telemetryJson = JsonSerializer.Serialize(request);
        Console.WriteLine($"Telemetry JSON: {telemetryJson}");

            // Create RepairPlanner agent with Cosmos DB tools
        var aoaiEndpoint = config["AZURE_OPENAI_ENDPOINT"];
        var aoaiDeployment = config["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o";
        var cosmosEndpoint = config["COSMOS_ENDPOINT"];
        var cosmosKey = config["COSMOS_KEY"];
        var cosmosDatabase = config["COSMOS_DATABASE"] ?? "FactoryOpsDB";
        

        // Create list of agents for the workflow
        var agents = new List<AIAgent> { anomalyClassificationAgent, faultDiagnosisAgent };

         if (!string.IsNullOrEmpty(aoaiEndpoint))
        {
            try
            {
                // Create Cosmos DB service for the tools (if configured)
                CosmosDbService? cosmosService = null;
                if (!string.IsNullOrEmpty(cosmosEndpoint) && !string.IsNullOrEmpty(cosmosKey))
                {
                    cosmosService = new CosmosDbService(
                        cosmosEndpoint, cosmosKey, cosmosDatabase,
                        loggerFactory.CreateLogger<CosmosDbService>());
                    Console.WriteLine("CosmosDbService created for RepairPlannerAgent tools");
                }
                
                var repairPlannerAgent = RepairPlannerAgentFactory.Create(
                    aoaiEndpoint, aoaiDeployment, cosmosService, loggerFactory);
                
                // Insert after FaultDiagnosis (position 2, index 2)
                agents.Add(repairPlannerAgent);
                Console.WriteLine($"RepairPlannerAgent created at {aoaiEndpoint}");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not create RepairPlannerAgent - skipping");
            }
        }
        else
        {
            logger.LogWarning("AZURE_OPENAI_ENDPOINT not configured - RepairPlannerAgent will be skipped");
        }

        // Add A2A agents from Python app if URLs are configured
        var maintenanceSchedulerUrl = config["MAINTENANCE_SCHEDULER_AGENT_URL"];
        if (!string.IsNullOrEmpty(maintenanceSchedulerUrl))
        {
            var cardResolver = new A2ACardResolver(new Uri(maintenanceSchedulerUrl + "/"));
            var maintenanceSchedulerAgent = await cardResolver.GetAIAgentAsync();
            agents.Add(maintenanceSchedulerAgent);
            Console.WriteLine($"A2A Agent added: {maintenanceSchedulerAgent.Name} at {maintenanceSchedulerUrl}");
        }

        var partsOrderingUrl = config["PARTS_ORDERING_AGENT_URL"];
        if (!string.IsNullOrEmpty(partsOrderingUrl))
        {
            var cardResolver = new A2ACardResolver(new Uri(partsOrderingUrl + "/"));
            var partsOrderingAgent = await cardResolver.GetAIAgentAsync();
            agents.Add(partsOrderingAgent);
            Console.WriteLine($"A2A Agent added: {partsOrderingAgent.Name} at {partsOrderingUrl}");
        }

    
       

        var workflow = AgentWorkflowBuilder.BuildSequential(agents.ToArray());
        var workflowResult = new WorkflowResponse();
        string? currentAgentName = null;
        AgentStepResult? currentAgentStep = null;

        var run = await InProcessExecution.RunAsync(workflow, telemetryJson);

        foreach (var evt in run.NewEvents)
        {
            // Track when an executor (agent) starts
            if (evt is ExecutorInvokedEvent executorInvoked)
            {
                // Save previous agent step if exists
                if (currentAgentStep != null)
                {
                    workflowResult.AgentSteps.Add(currentAgentStep);
                }
                currentAgentName = executorInvoked.ExecutorId ?? "UnknownAgent";
                currentAgentStep = new AgentStepResult { AgentName = currentAgentName };
                logger.LogInformation("Agent started: {AgentName}", currentAgentName);
            }
            else if (evt is ExecutorCompletedEvent executorCompleted)
            {
                if (currentAgentStep != null)
                {
                    // Capture final message from this agent if available
                    currentAgentStep.FinalMessage = executorCompleted.Data?.ToString();
                }
                logger.LogInformation("Agent completed: {AgentName}", currentAgentName);
            }
            else if (evt is AgentRunUpdateEvent e)
            {
                if (e.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
                {
                    var toolCall = new ToolCallInfo
                    {
                        ToolName = call.Name,
                        Arguments = call.Arguments != null 
                            ? JsonSerializer.Serialize(call.Arguments) 
                            : null
                    };
                    currentAgentStep?.ToolCalls.Add(toolCall);
                    logger.LogInformation(
                        "Calling function '{CallName}' with arguments: {Args}",
                        call.Name,
                        toolCall.Arguments);
                }
                else if (e.Update.Contents.OfType<FunctionResultContent>().FirstOrDefault() is FunctionResultContent funcResult)
                {
                    // Match result to the last tool call for this agent
                    var lastToolCall = currentAgentStep?.ToolCalls.LastOrDefault();
                    if (lastToolCall != null)
                    {
                        lastToolCall.Result = funcResult.Result?.ToString();
                    }
                    logger.LogInformation("Function result received for {ToolName}", lastToolCall?.ToolName);
                }
#pragma warning disable MEAI001 // Evaluation-only API; suppress to allow compilation.
                else if (e.Update.Contents.OfType<Microsoft.Extensions.AI.McpServerToolCallContent>().FirstOrDefault() is McpServerToolCallContent mcpCall)
                {
                    var toolCall = new ToolCallInfo
                    {
                        ToolName = mcpCall.ToolName,
                        Arguments = mcpCall.Arguments != null 
                            ? JsonSerializer.Serialize(mcpCall.Arguments) 
                            : null
                    };
                    currentAgentStep?.ToolCalls.Add(toolCall);
                    logger.LogInformation(
                        "Calling MCP function '{CallName}' with arguments: {Args}",
                        mcpCall.ToolName,
                        toolCall.Arguments);
                }
                else if(e.Update.Contents.OfType<Microsoft.Extensions.AI.McpServerToolResultContent>().FirstOrDefault() is McpServerToolResultContent mcpCallResult)
                {
                    var lastToolCall = currentAgentStep?.ToolCalls.LastOrDefault();
                    if (lastToolCall != null)
                    {
                        lastToolCall.Result = mcpCallResult.Output?.ToString();
                    }
                    logger.LogInformation(
                        "MCP Function result: {Message}",
                        mcpCallResult.Output);
                }
#pragma warning restore MEAI001
                // Capture text content updates - concatenate streamed tokens
                else if (e.Update.Contents.OfType<TextContent>().FirstOrDefault() is TextContent textContent)
                {
                    if (currentAgentStep != null && textContent.Text != null)
                    {
                        currentAgentStep.TextOutput += textContent.Text;
                    }
                }
            }
            
            else if (evt is WorkflowOutputEvent output)
            {
                // Save the last agent step
                if (currentAgentStep != null)
                {
                    workflowResult.AgentSteps.Add(currentAgentStep);
                    currentAgentStep = null;
                }

                foreach (var msg in evt.Data as List<Microsoft.Extensions.AI.ChatMessage> ?? new List<Microsoft.Extensions.AI.ChatMessage>())
                {
                    if (msg.Role == ChatRole.Assistant)
                    {
                        foreach (Microsoft.Extensions.AI.AIContent content in msg.Contents)
                        {
                            if (content is TextContent tc && !string.IsNullOrWhiteSpace(tc.Text))
                            {
                                workflowResult.FinalMessage = tc.Text;
                            }
                        }
                    }
                }
            }
        }

        // If there's still an unsaved agent step, add it
        if (currentAgentStep != null)
        {
            workflowResult.AgentSteps.Add(currentAgentStep);
        }

        return Results.Ok(workflowResult);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Workflow failed");
        return Results.Problem(ex.Message);
    }
}

/// <summary>
/// Streaming endpoint that sends workflow events as Server-Sent Events (SSE).
/// Events are streamed in near real-time as agents process the workflow.
/// </summary>
static async Task AnalyzeMachineStream(
    AnalyzeRequest request,
    AIProjectClient projectClient,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILoggerFactory loggerFactory,
    ILogger<Program> logger,
    HttpContext httpContext)
{
    logger.LogInformation("Starting streaming analysis for machine {MachineId}", request.machine_id);

    // Set up SSE response headers
    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";
    
    var jsonOptions = new JsonSerializerOptions 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false 
    };

    try
    {
        AIAgent anomalyClassificationAgent = projectClient.GetAIAgent("AnomalyClassificationAgent");
        AIAgent faultDiagnosisAgent = projectClient.GetAIAgent("FaultDiagnosisAgent");

        var telemetryJson = JsonSerializer.Serialize(request);

        // Create list of agents for the workflow
        var agents = new List<AIAgent> { anomalyClassificationAgent, faultDiagnosisAgent };

        var aoaiEndpoint = config["AZURE_OPENAI_ENDPOINT"];
        var aoaiDeployment = config["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o";
        var cosmosEndpoint = config["COSMOS_ENDPOINT"];
        var cosmosKey = config["COSMOS_KEY"];
        var cosmosDatabase = config["COSMOS_DATABASE"] ?? "FactoryOpsDB";

        if (!string.IsNullOrEmpty(aoaiEndpoint))
        {
            try
            {
                CosmosDbService? cosmosService = null;
                if (!string.IsNullOrEmpty(cosmosEndpoint) && !string.IsNullOrEmpty(cosmosKey))
                {
                    cosmosService = new CosmosDbService(
                        cosmosEndpoint, cosmosKey, cosmosDatabase,
                        loggerFactory.CreateLogger<CosmosDbService>());
                }

                var repairPlannerAgent = RepairPlannerAgentFactory.Create(
                    aoaiEndpoint, aoaiDeployment, cosmosService, loggerFactory);
                agents.Add(repairPlannerAgent);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not create RepairPlannerAgent - skipping");
            }
        }

        // Add A2A agents if configured
        var maintenanceSchedulerUrl = config["MAINTENANCE_SCHEDULER_AGENT_URL"];
        if (!string.IsNullOrEmpty(maintenanceSchedulerUrl))
        {
            var cardResolver = new A2ACardResolver(new Uri(maintenanceSchedulerUrl + "/"));
            var maintenanceSchedulerAgent = await cardResolver.GetAIAgentAsync();
            agents.Add(maintenanceSchedulerAgent);
        }

        var partsOrderingUrl = config["PARTS_ORDERING_AGENT_URL"];
        if (!string.IsNullOrEmpty(partsOrderingUrl))
        {
            var cardResolver = new A2ACardResolver(new Uri(partsOrderingUrl + "/"));
            var partsOrderingAgent = await cardResolver.GetAIAgentAsync();
            agents.Add(partsOrderingAgent);
        }

        var workflow = AgentWorkflowBuilder.BuildSequential(agents.ToArray());
        
        // Use streaming execution - StreamAsync returns a StreamingRun
        await using var run = await InProcessExecution.StreamAsync(workflow, telemetryJson);
        
        // Send a TurnToken to start the workflow with event emission enabled
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        
        string? currentAgentName = null;
        var currentAgentStep = new StreamingAgentStep();
        
        // Watch the stream and emit events as they happen
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(httpContext.RequestAborted))
        {
            // Track when an executor (agent) starts
            if (evt is ExecutorInvokedEvent executorInvoked)
            {
                // If we have a previous agent step with content, send it as complete
                if (!string.IsNullOrEmpty(currentAgentStep.AgentName) && 
                    (currentAgentStep.ToolCalls.Count > 0 || !string.IsNullOrEmpty(currentAgentStep.TextOutput)))
                {
                    currentAgentStep.Status = "done";
                    await SendSseEventAsync(httpContext, "agent_complete", currentAgentStep, jsonOptions);
                }
                
                currentAgentName = executorInvoked.ExecutorId ?? "UnknownAgent";
                currentAgentStep = new StreamingAgentStep { AgentName = currentAgentName, Status = "running" };
                
                await SendSseEventAsync(httpContext, "agent_started", 
                    new { agentName = currentAgentName }, jsonOptions);
                
                logger.LogInformation("Streaming: Agent started: {AgentName}", currentAgentName);
            }
            else if (evt is ExecutorCompletedEvent executorCompleted)
            {
                currentAgentStep.FinalMessage = executorCompleted.Data?.ToString();
                currentAgentStep.Status = "done";
                
                await SendSseEventAsync(httpContext, "agent_complete", currentAgentStep, jsonOptions);
                
                logger.LogInformation("Streaming: Agent completed: {AgentName}", currentAgentName);
                
                // Reset for next agent
                currentAgentStep = new StreamingAgentStep();
            }
            else if (evt is AgentRunUpdateEvent e)
            {
                if (e.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
                {
                    var toolCall = new ToolCallInfo
                    {
                        ToolName = call.Name,
                        Arguments = call.Arguments != null 
                            ? JsonSerializer.Serialize(call.Arguments) 
                            : null
                    };
                    currentAgentStep.ToolCalls.Add(toolCall);
                    
                    await SendSseEventAsync(httpContext, "tool_call", new 
                    { 
                        agentName = currentAgentName, 
                        toolName = call.Name,
                        arguments = toolCall.Arguments 
                    }, jsonOptions);
                    
                    logger.LogInformation("Streaming: Tool call '{ToolName}'", call.Name);
                }
                else if (e.Update.Contents.OfType<FunctionResultContent>().FirstOrDefault() is FunctionResultContent funcResult)
                {
                    var lastToolCall = currentAgentStep.ToolCalls.LastOrDefault();
                    if (lastToolCall != null)
                    {
                        lastToolCall.Result = funcResult.Result?.ToString();
                        
                        await SendSseEventAsync(httpContext, "tool_result", new 
                        { 
                            agentName = currentAgentName, 
                            toolName = lastToolCall.ToolName,
                            result = lastToolCall.Result 
                        }, jsonOptions);
                    }
                }
                // MEAI001: McpServerToolCallContent and McpServerToolResultContent are evaluation-only APIs.
                // Suppressing to allow MCP tool call/result tracking in streaming workflow events.
#pragma warning disable MEAI001
                else if (e.Update.Contents.OfType<Microsoft.Extensions.AI.McpServerToolCallContent>().FirstOrDefault() is McpServerToolCallContent mcpCall)
                {
                    var toolCall = new ToolCallInfo
                    {
                        ToolName = mcpCall.ToolName,
                        Arguments = mcpCall.Arguments != null 
                            ? JsonSerializer.Serialize(mcpCall.Arguments) 
                            : null
                    };
                    currentAgentStep.ToolCalls.Add(toolCall);
                    
                    await SendSseEventAsync(httpContext, "tool_call", new 
                    { 
                        agentName = currentAgentName, 
                        toolName = mcpCall.ToolName,
                        arguments = toolCall.Arguments 
                    }, jsonOptions);
                }
                else if (e.Update.Contents.OfType<Microsoft.Extensions.AI.McpServerToolResultContent>().FirstOrDefault() is McpServerToolResultContent mcpCallResult)
                {
                    var lastToolCall = currentAgentStep.ToolCalls.LastOrDefault();
                    if (lastToolCall != null)
                    {
                        lastToolCall.Result = mcpCallResult.Output?.ToString();
                        
                        await SendSseEventAsync(httpContext, "tool_result", new 
                        { 
                            agentName = currentAgentName, 
                            toolName = lastToolCall.ToolName,
                            result = lastToolCall.Result 
                        }, jsonOptions);
                    }
                }
#pragma warning restore MEAI001
                else if (e.Update.Contents.OfType<TextContent>().FirstOrDefault() is TextContent textContent)
                {
                    if (textContent.Text != null)
                    {
                        currentAgentStep.TextOutput += textContent.Text;
                        
                        // Stream text tokens as they arrive
                        await SendSseEventAsync(httpContext, "text_token", new 
                        { 
                            agentName = currentAgentName, 
                            text = textContent.Text 
                        }, jsonOptions);
                    }
                }
            }
            else if (evt is WorkflowOutputEvent output)
            {
                // Send final agent step if it has content
                if (!string.IsNullOrEmpty(currentAgentStep.AgentName) && 
                    (currentAgentStep.ToolCalls.Count > 0 || !string.IsNullOrEmpty(currentAgentStep.TextOutput)))
                {
                    currentAgentStep.Status = "done";
                    await SendSseEventAsync(httpContext, "agent_complete", currentAgentStep, jsonOptions);
                }

                string? finalMessage = null;
                var messages = evt.Data as List<Microsoft.Extensions.AI.ChatMessage> 
                    ?? new List<Microsoft.Extensions.AI.ChatMessage>();
                foreach (var msg in messages)
                {
                    if (msg.Role == ChatRole.Assistant)
                    {
                        foreach (Microsoft.Extensions.AI.AIContent content in msg.Contents)
                        {
                            if (content is TextContent tc && !string.IsNullOrWhiteSpace(tc.Text))
                            {
                                finalMessage = tc.Text;
                            }
                        }
                    }
                }
                
                await SendSseEventAsync(httpContext, "workflow_complete", new 
                { 
                    finalMessage 
                }, jsonOptions);
            }
        }
        
        // Send done event to signal end of stream
        await SendSseEventAsync(httpContext, "done", new { }, jsonOptions);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Streaming was cancelled for machine {MachineId}", request.machine_id);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Streaming workflow failed");
        await SendSseEventAsync(httpContext, "error", new { message = ex.Message }, 
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}

/// <summary>
/// Helper method to send an SSE event to the client.
/// </summary>
static async Task SendSseEventAsync<T>(HttpContext context, string eventType, T data, JsonSerializerOptions jsonOptions)
{
    var json = JsonSerializer.Serialize(data, jsonOptions);
    var sseMessage = $"event: {eventType}\ndata: {json}\n\n";
    await context.Response.WriteAsync(sseMessage);
    await context.Response.Body.FlushAsync();
}

static class Workflow
{
    internal static string ExtractText(AgentRunResponse response)
    {
        var last = response.Messages.LastOrDefault();
        return last?.Text ?? string.Empty;
    }

    internal static bool CheckDiagnosisCondition(string text)
    {
        var keywords = new[] { "critical", "warning", "high", "alert" };
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    internal static async Task<string?> TryResolveAgentIdByNameAsync(AIProjectClient projectClient, string agentName, CancellationToken cancellationToken)
    {
        await foreach (var agent in projectClient.Agents.GetAgentsAsync(cancellationToken: cancellationToken))
        {
            if (string.Equals(agent.Name, agentName, StringComparison.OrdinalIgnoreCase))
            {
                return agent.Id;
            }
        }
        return null;
    }

    internal static async Task<string> InvokeRepairPlannerAsync(
        HttpClient httpClient,
        string baseUrl,
        string machineId,
        string diagnosedFault,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { input = diagnosedFault, machine_id = machineId });
        var url = baseUrl.TrimEnd('/') + "/process";

        logger.LogInformation("Invoking RepairPlannerAgent at {Url}", url);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return $"Error: {(int)response.StatusCode} {response.ReasonPhrase} - {body}";
        }

        return body;
    }
}

record AnalyzeRequest(string machine_id, JsonElement telemetry);

/// <summary>
/// Represents the complete workflow response with detailed agent execution info
/// </summary>
public class WorkflowResponse
{
    public List<AgentStepResult> AgentSteps { get; set; } = new();
    public string? FinalMessage { get; set; }
}

/// <summary>
/// Represents the execution details of a single agent in the workflow
/// </summary>
public class AgentStepResult
{
    public string AgentName { get; set; } = string.Empty;
    public List<ToolCallInfo> ToolCalls { get; set; } = new();
    public string TextOutput { get; set; } = string.Empty;
    public string? FinalMessage { get; set; }
}

/// <summary>
/// Represents a tool/function call made by an agent
/// </summary>
public class ToolCallInfo
{
    public string ToolName { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? Result { get; set; }
}

/// <summary>
/// Represents a streaming agent step with real-time status
/// </summary>
public class StreamingAgentStep
{
    public string AgentName { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public List<ToolCallInfo> ToolCalls { get; set; } = new();
    public string TextOutput { get; set; } = string.Empty;
    public string? FinalMessage { get; set; }
}
