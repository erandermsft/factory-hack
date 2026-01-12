using A2A;
using A2A.AspNetCore;
// using OpenTelemetry.Resources;
// using OpenTelemetry.Trace;


var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry
// builder.Services.AddOpenTelemetry()
//     .ConfigureResource(resource => resource.AddService("A2AAgentServer"))
//     .WithTracing(tracing => tracing
//         .AddSource(TaskManager.ActivitySource.Name)
//         .AddSource(A2AJsonRpcProcessor.ActivitySource.Name)
//         .AddSource(ResearcherAgent.ActivitySource.Name)
//         .AddAspNetCoreInstrumentation()
//         .AddHttpClientInstrumentation()
//         .AddConsoleExporter()
//         .AddOtlpExporter(options =>
//         {
//             options.Endpoint = new Uri("http://localhost:4317");
//             options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
//         })
//     );

var app = builder.Build();

//app.UseHttpsRedirection();

var taskManager = new TaskManager();

var echoAgent = new EchoAgent();
    echoAgent.Attach(taskManager);
    app.MapA2A(taskManager, "/echo");
    app.MapWellKnownAgentCard(taskManager, "/echo");
    app.MapHttpA2A(taskManager, "/echo");

// Add health endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow }));
app.Run();