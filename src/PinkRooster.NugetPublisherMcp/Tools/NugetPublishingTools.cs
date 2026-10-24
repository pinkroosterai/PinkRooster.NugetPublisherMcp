using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PinkRooster.NugetPublisher.Publishing;

namespace PinkRooster.NugetPublisherMcp.Tools;

/// <summary>
/// Provides MCP tools for publishing NuGet packages and companion symbol archives.
/// </summary>
[McpServerToolType]
[SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "MCP tool method names match protocol tool identifiers."
)]
public sealed class NugetPublishingTools
{
    private readonly IPackagePublisherService _publisherService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NugetPublishingTools"/> class.
    /// </summary>
    /// <param name="publisherService">The package publisher service.</param>
    public NugetPublishingTools(IPackagePublisherService publisherService)
    {
        ArgumentNullException.ThrowIfNull(publisherService);
        _publisherService = publisherService;
    }

    /// <summary>
    /// Safely publishes a .nupkg package archive and companion .snupkg symbols with pre-flight safety validations.
    /// </summary>
    /// <param name="nupkgPath">Absolute or relative path to the .nupkg package archive.</param>
    /// <param name="feedUrl">Target NuGet v3 service index URL or v2 push endpoint.</param>
    /// <param name="apiKey">Explicit API key or PAT; if omitted, resolved from ambient env or nuget.config.</param>
    /// <param name="symbolFeedUrl">Custom symbol publish URL; if omitted, discovered from the service index.</param>
    /// <param name="skipDuplicate">If true, version conflict (409) is treated as non-fatal.</param>
    /// <param name="force">Bypass pre-flight warnings (cannot bypass hard feed rejection errors).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="CallToolResult"/> detailing publication outcome and diagnostics.</returns>
    [McpServerTool(Name = "nuget_publish", Destructive = true)]
    [Description(
        "Safely publish a .nupkg archive (and optional adjacent companion .snupkg) to nuget.org or a private NuGet v3 feed."
    )]
    public async Task<CallToolResult> nuget_publish(
        [Description("Absolute or relative path to the .nupkg package archive.")] string nupkgPath,
        [Description("Target NuGet v3 service index URL or v2 push endpoint.")]
            string? feedUrl = null,
        [Description(
            "Explicit API key or PAT; if omitted, resolved from ambient env or nuget.config."
        )]
            string? apiKey = null,
        [Description("Custom symbol publish URL; if omitted, discovered from the service index.")]
            string? symbolFeedUrl = null,
        [Description("If true, version conflict (409) is treated as non-fatal.")]
            bool skipDuplicate = false,
        [Description("Bypass pre-flight warnings (cannot bypass hard feed rejection errors).")]
            bool force = false,
        CancellationToken ct = default
    )
    {
        try
        {
            PublishPackageRequest request = new(nupkgPath)
            {
                FeedUrl = feedUrl,
                ApiKey = apiKey,
                SymbolFeedUrl = symbolFeedUrl,
                SkipDuplicate = skipDuplicate,
                Force = force,
            };

            PublishPackageResponse response = await _publisherService.PublishAsync(request, ct);
            return ToolResultHelper.CreateResult(response.Success, response, apiKey);
        }
        catch (Exception ex)
        {
            return ToolResultHelper.CreateErrorResult(ex, apiKey);
        }
    }
}
