using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.AI.Agents.Persistent;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AzureAI;
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

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
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

var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];

var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(SourceName, "ChatClient") // Our custom activity source(s)
    .AddSource("Microsoft.Agents.AI*") // Agent Framework telemetry
    .AddSource("AnomalyClassificationAgent", "FaultDiagnosisAgent", "RepairPlannerAgent") // Our agents
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
app.MapPost("/api/analyze_machine", AnalyzeMachine);
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow }));
app.Run();

static async Task<IResult> AnalyzeMachine(
    AnalyzeRequest request,
    AIProjectClient projectClient,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<Program> logger)
{
    logger.LogInformation("Starting analysis for machine {MachineId}", request.machine_id);

    try
    {
        AIAgent anomalyClassificationAgent = projectClient.GetAIAgent("AnomalyClassificationAgent");
        AIAgent faultDiagnosisAgent = projectClient.GetAIAgent("FaultDiagnosisAgent");

        Console.WriteLine($"Agent retrieved (name: {faultDiagnosisAgent.Name}, id: {faultDiagnosisAgent.Id})");
        Console.WriteLine($"Agent retrieved (name: {anomalyClassificationAgent.Name}, id: {anomalyClassificationAgent.Id})");
        
        var telemetryJson = request.telemetry.ValueKind == JsonValueKind.Undefined
            ? string.Empty
            : request.telemetry.GetRawText();

        var workflow = AgentWorkflowBuilder.BuildSequential(anomalyClassificationAgent, faultDiagnosisAgent);
        var result = new List<Microsoft.Extensions.AI.ChatMessage>();

        var run = await InProcessExecution.RunAsync(workflow, telemetryJson);

        foreach (var evt in run.NewEvents)
        {
            if (evt is AgentRunUpdateEvent e)
            {
                // if (e.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
                // {
                //     logger.LogInformation(
                //         "Calling function '{CallName}' with arguments: {Args}",
                //         call.Name,
                //         JsonSerializer.Serialize(call.Arguments));
                // }
            }
            else if (evt is WorkflowOutputEvent output)
            {
                Console.WriteLine(evt.Data);
                foreach (var msg in evt.Data as List<Microsoft.Extensions.AI.ChatMessage> ?? new List<Microsoft.Extensions.AI.ChatMessage>())
                {
                    Console.WriteLine($"{msg.Role}:");
                    foreach(Microsoft.Extensions.AI.AIContent content in msg.Contents)
                    {
                      
                        Console.WriteLine(content.ToString());
                    }
                    // Console.WriteLine($"{msg.Role}: {msg.Contents}"); 
                }
                // result.Add(evt.Data); 
                // result = evt.Data as List<Microsoft.Extensions.AI.ChatMessage> ?? new List<Microsoft.Extensions.AI.ChatMessage>();  
            }
        }

        return Results.Ok(result);
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
