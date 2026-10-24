using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Feeds.Models;
using PinkRooster.NugetPublisher.Vulnerabilities;

namespace PinkRooster.NugetPublisherMcp.Tools;

/// <summary>
/// Provides MCP tools for searching packages, retrieving metadata, and auditing security vulnerabilities.
/// </summary>
[McpServerToolType]
[SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "MCP tool method names match protocol tool identifiers."
)]
public sealed class NugetDiscoveryTools
{
    private readonly INugetFeedClient _feedClient;
    private readonly IVulnerabilityAuditService _vulnerabilityService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NugetDiscoveryTools"/> class.
    /// </summary>
    /// <param name="feedClient">The NuGet feed client.</param>
    /// <param name="vulnerabilityService">The vulnerability audit service.</param>
    public NugetDiscoveryTools(
        INugetFeedClient feedClient,
        IVulnerabilityAuditService vulnerabilityService
    )
    {
        ArgumentNullException.ThrowIfNull(feedClient);
        ArgumentNullException.ThrowIfNull(vulnerabilityService);
        _feedClient = feedClient;
        _vulnerabilityService = vulnerabilityService;
    }

    /// <summary>
    /// Searches packages on the specified NuGet feed matching the query term, ID prefix, or keyword.
    /// </summary>
    /// <param name="query">The search term, package ID prefix, or keyword.</param>
    /// <param name="take">Maximum number of results to return (default: 20).</param>
    /// <param name="prerelease">Whether to include pre-release versions.</param>
    /// <param name="feedUrl">Target feed service index URL, or null for default nuget.org.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="CallToolResult"/> containing matching package search results.</returns>
    [McpServerTool(Name = "nuget_search", ReadOnly = true)]
    [Description("Search packages on NuGet feeds by query, ID prefix, or tags.")]
    public async Task<CallToolResult> nuget_search(
        [Description("Search term, package ID prefix, or keyword.")] string query,
        [Description("Maximum number of results to return (default: 20).")] int take = 20,
        [Description("Whether to include pre-release versions.")] bool prerelease = false,
        [Description("Target feed service index URL, or null for default nuget.org.")]
            string? feedUrl = null,
        CancellationToken ct = default
    )
    {
        try
        {
            IReadOnlyList<PackageSearchResult> results = await _feedClient.SearchPackagesAsync(
                query,
                take,
                prerelease,
                feedUrl,
                ct
            );
            return ToolResultHelper.CreateSuccessResult(results);
        }
        catch (Exception ex)
        {
            return ToolResultHelper.CreateErrorResult(ex);
        }
    }

    /// <summary>
    /// Retrieves aggregate package metadata, version listings, and dependencies from the target feed registration index.
    /// </summary>
    /// <param name="packageId">The unique package identifier.</param>
    /// <param name="feedUrl">Target feed service index URL, or null for default nuget.org.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="CallToolResult"/> containing package registration metadata.</returns>
    [McpServerTool(Name = "nuget_get_metadata", ReadOnly = true)]
    [Description(
        "Retrieve full registration details: versions, authors, tags, license, repository URLs, and dependencies per target framework."
    )]
    public async Task<CallToolResult> nuget_get_metadata(
        [Description("The unique package identifier.")] string packageId,
        [Description("Target feed service index URL, or null for default nuget.org.")]
            string? feedUrl = null,
        CancellationToken ct = default
    )
    {
        try
        {
            PackageMetadataResult metadata = await _feedClient.GetPackageMetadataAsync(
                packageId,
                feedUrl,
                ct
            );
            return ToolResultHelper.CreateSuccessResult(metadata);
        }
        catch (Exception ex)
        {
            return ToolResultHelper.CreateErrorResult(ex);
        }
    }

    /// <summary>
    /// Queries known security advisories and deprecation records for a package or specific package version.
    /// </summary>
    /// <param name="packageId">The unique package identifier to audit.</param>
    /// <param name="version">Specific package version to audit, or null to audit all published versions.</param>
    /// <param name="feedUrl">Target feed service index URL, or null for default nuget.org.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="CallToolResult"/> containing the vulnerability audit report.</returns>
    [McpServerTool(Name = "nuget_audit_vulnerabilities", ReadOnly = true)]
    [Description(
        "Query known CVEs and security advisories for a package/version across NuGet v3 and advisory databases."
    )]
    public async Task<CallToolResult> nuget_audit_vulnerabilities(
        [Description("The unique package identifier to audit.")] string packageId,
        [Description("Specific package version to audit, or null to audit all published versions.")]
            string? version = null,
        [Description("Target feed service index URL, or null for default nuget.org.")]
            string? feedUrl = null,
        CancellationToken ct = default
    )
    {
        try
        {
            VulnerabilityAuditReport report = await _vulnerabilityService.AuditPackageAsync(
                packageId,
                version,
                feedUrl,
                ct
            );
            return ToolResultHelper.CreateSuccessResult(report);
        }
        catch (Exception ex)
        {
            return ToolResultHelper.CreateErrorResult(ex);
        }
    }
}
