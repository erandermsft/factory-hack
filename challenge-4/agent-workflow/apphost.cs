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
var app = builder.AddUvicornApp("app", "./app", "main:app")
    .WithUv()
    .WithEnvironment("REPAIR_PLANNER_AGENT_URL", dotnetapp.GetEndpoint("https"))
    .WithHttpEndpoint(port: 8000, env: "UVICORN_PORT", name: "api")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

var dotnetworkflow = builder.AddCSharpApp("dotnetworkflow", "./dotnetworkflow/FactoryWorkflow.csproj")
    .WithExternalHttpEndpoints()
    // .WithEnvironment("SSL_CERT_DIR", "/etc/ssl/certs")
    .WithHttpEndpoint(port:8001, env: "DOTNETWORKFLOW_PORT", name: "api")
    .WithEnvironment("SSL_CERT_FILE", "/etc/ssl/certs/ca-certificates.crt")
    .WithHttpHealthCheck("/health");
#pragma warning restore ASPIRECSHARPAPPS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
// Use the externally reachable HTTP endpoint for the frontend (Aspire also creates an internal/proxied endpoint).
var apiEndpoint = dotnetworkflow.GetEndpoint("http")
    ?? throw new InvalidOperationException("Expected 'http' endpoint for 'dotnetworkflow' was not created.");
var frontend = builder.AddViteApp("frontend", "./frontend")
    .WithReference(dotnetworkflow)
    .WithReference(app)
    .WithEnvironment("VITE_API_URL", () =>
    {
        var codespaceName = Environment.GetEnvironmentVariable("CODESPACE_NAME");
        var codespacesPortForwardingDomain = Environment.GetEnvironmentVariable("GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN");

        if (!string.IsNullOrWhiteSpace(codespaceName) && !string.IsNullOrWhiteSpace(codespacesPortForwardingDomain))
        {
            return $"https://{codespaceName}-{apiEndpoint.Port}.{codespacesPortForwardingDomain}/";
        }

        var baseUrl = (apiEndpoint.ToString() ?? throw new InvalidOperationException("dotnetworkflow endpoint URL was null.")).TrimEnd('/');
        return $"{baseUrl}/";
    })
    .WaitFor(app)
    .WaitFor(dotnetworkflow);

app.PublishWithContainerFiles(frontend, "./static");

builder.Build().Run();
