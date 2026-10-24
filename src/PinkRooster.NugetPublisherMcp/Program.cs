using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using PinkRooster.NugetPublisher.Administration;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Publishing;
using PinkRooster.NugetPublisher.Validation;
using PinkRooster.NugetPublisher.Vulnerabilities;
using PinkRooster.NugetPublisherMcp.Tools;

namespace PinkRooster.NugetPublisherMcp;

/// <summary>
/// Configures and runs the PinkRooster NuGet Publisher MCP stdio server host.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Application entry point. Starts the MCP server over standard I/O.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task representing the server execution lifetime.</returns>
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = CreateHostBuilder(args);
        IHost host = builder.Build();
        await host.RunAsync();
    }

    /// <summary>
    /// Creates and configures the <see cref="HostApplicationBuilder"/> with MCP stdio transport and domain services.
    /// </summary>
    /// <param name="args">Optional command-line arguments.</param>
    /// <returns>A configured <see cref="HostApplicationBuilder"/> instance.</returns>
    public static HostApplicationBuilder CreateHostBuilder(string[]? args = null)
    {
        HostApplicationBuilder builder = new(args);

        // Standard output (Console.Out) is dedicated strictly to MCP JSON-RPC protocol messages.
        // Route all application diagnostics and logging to standard error.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Register domain services into DI
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<INugetFeedClient, NugetFeedClient>();
        builder.Services.AddSingleton<PublishSafetyValidator>();
        builder.Services.AddSingleton<IPackagePublisherService, PackagePublisherService>();
        builder.Services.AddSingleton<
            IPackageAdministrationService,
            PackageAdministrationService
        >();
        builder.Services.AddSingleton<IVulnerabilityAuditService, VulnerabilityAuditService>();

        // Register tool classes into DI
        builder.Services.AddSingleton<NugetDiscoveryTools>();
        builder.Services.AddSingleton<NugetInspectionTools>();
        builder.Services.AddSingleton<NugetPublishingTools>();
        builder.Services.AddSingleton<NugetAdministrationTools>();

        // Configure MCP server with stdio transport and granular tools
        builder
            .Services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "PinkRooster.NugetPublisherMcp",
                    Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
                };
            })
            .WithStdioServerTransport()
            .WithTools<NugetDiscoveryTools>()
            .WithTools<NugetInspectionTools>()
            .WithTools<NugetPublishingTools>()
            .WithTools<NugetAdministrationTools>();

        return builder;
    }
}
