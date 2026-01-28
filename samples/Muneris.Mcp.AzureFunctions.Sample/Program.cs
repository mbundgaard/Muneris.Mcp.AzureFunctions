using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muneris.Mcp.AzureFunctions.Extensions;
using Muneris.Mcp.AzureFunctions.Sample.Auth;
using Muneris.Mcp.AzureFunctions.Sample.Resources;
using Muneris.Mcp.AzureFunctions.Sample.Tools;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddMcp(mcp =>
        {
            mcp.Configure(options =>
            {
                options.ServerName = "Muneris MCP Sample";
                options.ServerVersion = "1.0.0";
                options.Instructions = "This is a sample MCP server demonstrating the Muneris.Mcp.AzureFunctions package.";
            });

            mcp.AddToolsFromType<SampleTools>();
            mcp.AddResourcesFromType<SampleResources>();
            mcp.AddAuthValidator<JwtBearerValidator>();
        });
    })
    .Build();

host.Run();
