#:sdk Aspire.AppHost.Sdk@13.1.0
#:package Aspire.Hosting.JavaScript@13.1.0
#:package Aspire.Hosting.Python@13.1.0

var builder = DistributedApplication.CreateBuilder(args);



#pragma warning disable ASPIRECSHARPAPPS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var dotnetapp = builder.AddCSharpApp("dotnetagent", "./a2a/dotnetagent.csproj")
    .WithExternalHttpEndpoints()
    .WithEnvironment("SSL_CERT_DIR", "/etc/ssl/certs")
    .WithEnvironment("SSL_CERT_FILE", "/etc/ssl/certs/ca-certificates.crt")
    .WithHttpHealthCheck("/health");
// var app = builder.AddUvicornApp("app", "./app", "main:app")
//     .WithUv()
//     .WithEnvironment("REPAIR_PLANNER_AGENT_URL", dotnetapp.GetEndpoint("https"))
//     .WithHttpEndpoint(port: 8000, env: "UVICORN_PORT", name: "api")
//     //.WithExternalHttpEndpoints(port:8000)
//     .WithHttpHealthCheck("/health");

var dotnetworkflow = builder.AddCSharpApp("dotnetworkflow", "./dotnetworkflow/FactoryWorkflow.csproj")
    .WithExternalHttpEndpoints()
    // .WithEnvironment("SSL_CERT_DIR", "/etc/ssl/certs")
    .WithEnvironment("SSL_CERT_FILE", "/etc/ssl/certs/ca-certificates.crt")
    .WithHttpHealthCheck("/health");
#pragma warning restore ASPIRECSHARPAPPS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.


// var frontend = builder.AddViteApp("frontend", "./frontend")
//     .WithReference(app)
//     .WaitFor(app);

// app.PublishWithContainerFiles(frontend, "./static");

builder.Build().Run();
