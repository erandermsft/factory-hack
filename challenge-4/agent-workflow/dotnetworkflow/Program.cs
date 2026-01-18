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
using FactoryWorkflow;

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
        // Get Azure AI Foundry agents (v2 Prompt Agents)
        AIAgent anomalyClassificationAgent = projectClient.GetAIAgent("AnomalyClassificationAgent");
        AIAgent faultDiagnosisAgent = projectClient.GetAIAgent("FaultDiagnosisAgent");

        Console.WriteLine($"Agent retrieved: {anomalyClassificationAgent.Name}");
        Console.WriteLine($"Agent retrieved: {faultDiagnosisAgent.Name}");
        
        var telemetryJson = JsonSerializer.Serialize(request);
        Console.WriteLine($"Telemetry JSON: {telemetryJson}");

        // Create RepairPlanner agent with Cosmos DB tools (local agent using AOAI directly)
        var aoaiEndpoint = config["AZURE_OPENAI_ENDPOINT"];
        var aoaiDeployment = config["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o";
        var cosmosEndpoint = config["COSMOS_ENDPOINT"];
        var cosmosKey = config["COSMOS_KEY"];
        var cosmosDatabase = config["COSMOS_DATABASE"] ?? "FactoryOpsDB";

        // Create list of agents for the workflow (sequential order)
        // 1. AnomalyClassificationAgent - classifies severity (Azure AI Foundry)
        // 2. FaultDiagnosisAgent - diagnoses root causes (Azure AI Foundry)
        // 3. RepairPlannerAgent - creates repair plan + work order (local AOAI)
        // 4. MaintenanceSchedulerAgent - schedules maintenance window (A2A Python)
        // 5. PartsOrderingAgent - orders required parts (A2A Python)
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

        // Add A2A agents from Python app (MaintenanceScheduler and PartsOrdering use local tools)
        var maintenanceSchedulerUrl = config["MAINTENANCE_SCHEDULER_AGENT_URL"];
        Console.WriteLine($"MAINTENANCE_SCHEDULER_AGENT_URL = '{maintenanceSchedulerUrl ?? "(not set)"}'");
        if (!string.IsNullOrEmpty(maintenanceSchedulerUrl))
        {
            try
            {
                var cardResolver = new A2ACardResolver(new Uri(maintenanceSchedulerUrl + "/"));
                var maintenanceSchedulerAgent = await cardResolver.GetAIAgentAsync();
                agents.Add(maintenanceSchedulerAgent);
                Console.WriteLine($"A2A Agent added: {maintenanceSchedulerAgent.Name} at {maintenanceSchedulerUrl}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve MaintenanceSchedulerAgent A2A at {Url}", maintenanceSchedulerUrl);
                Console.WriteLine($"ERROR: Failed to resolve MaintenanceSchedulerAgent: {ex.Message}");
            }
        }
        else
        {
            logger.LogWarning("MAINTENANCE_SCHEDULER_AGENT_URL not configured - MaintenanceSchedulerAgent will be skipped");
        }

        var partsOrderingUrl = config["PARTS_ORDERING_AGENT_URL"];
        Console.WriteLine($"PARTS_ORDERING_AGENT_URL = '{partsOrderingUrl ?? "(not set)"}'");
        if (!string.IsNullOrEmpty(partsOrderingUrl))
        {
            try
            {
                var cardResolver = new A2ACardResolver(new Uri(partsOrderingUrl + "/"));
                var partsOrderingAgent = await cardResolver.GetAIAgentAsync();
                agents.Add(partsOrderingAgent);
                Console.WriteLine($"A2A Agent added: {partsOrderingAgent.Name} at {partsOrderingUrl}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve PartsOrderingAgent A2A at {Url}", partsOrderingUrl);
                Console.WriteLine($"ERROR: Failed to resolve PartsOrderingAgent: {ex.Message}");
            }
        }
        else
        {
            logger.LogWarning("PARTS_ORDERING_AGENT_URL not configured - PartsOrderingAgent will be skipped");
        }

        Console.WriteLine($"Total agents in workflow: {agents.Count} - [{string.Join(", ", agents.Select(a => a.Name))}]");

        // Create custom executors that only pass TEXT between agents (strips MCP tool history)
        // This works around the Azure.AI.Projects SDK bug where MCP tool call history
        // causes deserialization errors in subsequent agents
        var executors = agents.Select(a => new TextOnlyAgentExecutor(a)).ToList();

        Console.WriteLine($"Building workflow with WorkflowBuilder using {executors.Count} TextOnlyAgentExecutors...");

        // Clear previous results before running
        TextOnlyAgentExecutor.ClearResults();

        // Build the workflow using WorkflowBuilder - chain executors sequentially
        // Each executor is Executor<string, string> so outputs chain directly to inputs
        var workflowBuilder = new WorkflowBuilder(executors[0]);
        for (int i = 1; i < executors.Count; i++)
        {
            var prevExecutor = executors[i - 1];
            var currExecutor = executors[i];
            // Bind next executor and add edge: prev output (string) -> curr input (string)
            workflowBuilder.BindExecutor(currExecutor);
            workflowBuilder.AddEdge(prevExecutor, currExecutor);
        }
        // Register last executor as output source (so we get the final result)
        workflowBuilder.WithOutputFrom(executors[^1]);
        var workflow = workflowBuilder.Build();
        Console.WriteLine("Workflow built successfully.");

        // Run the workflow using the InProcessExecution environment
        Console.WriteLine($"Starting workflow with input: {telemetryJson.Substring(0, Math.Min(100, telemetryJson.Length))}...");
        var run = await InProcessExecution.Default.RunAsync<string>(workflow, telemetryJson);

        // Get workflow final output from events
        string? finalOutput = null;
        foreach (var evt in run.OutgoingEvents)
        {
            Console.WriteLine($"Workflow event: {evt.GetType().Name}");
            if (evt is WorkflowOutputEvent outputEvent && outputEvent.Is<string>(out var text))
            {
                finalOutput = text;
                Console.WriteLine($"Workflow final output received: {text?.Substring(0, Math.Min(200, text?.Length ?? 0))}...");
            }
        }

        // Collect step results from the static collector
        var workflowResult = new WorkflowResponse
        {
            AgentSteps = TextOnlyAgentExecutor.StepResults.ToList(),
            FinalMessage = finalOutput ?? TextOnlyAgentExecutor.StepResults.LastOrDefault()?.FinalMessage
        };

        Console.WriteLine($"Workflow completed with {workflowResult.AgentSteps.Count} agent steps.");

        return Results.Ok(workflowResult);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Workflow failed");
        return Results.Problem(ex.Message);
    }
}

record AnalyzeRequest(string machine_id, JsonElement telemetry);

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
