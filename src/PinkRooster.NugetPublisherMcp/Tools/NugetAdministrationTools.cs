using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PinkRooster.NugetPublisher.Administration;

namespace PinkRooster.NugetPublisherMcp.Tools;

/// <summary>
/// Provides MCP tools for package visibility management (unlisting, relisting) and deprecation guidance.
/// </summary>
[McpServerToolType]
[SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "MCP tool method names match protocol tool identifiers."
)]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "MCP tool methods belong to tool instance class."
)]
public sealed class NugetAdministrationTools
{
    private readonly IPackageAdministrationService _adminService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NugetAdministrationTools"/> class.
    /// </summary>
    /// <param name="adminService">The package administration service.</param>
    public NugetAdministrationTools(IPackageAdministrationService adminService)
    {
        ArgumentNullException.ThrowIfNull(adminService);
        _adminService = adminService;
    }

    /// <summary>
    /// Administers the visibility and listing lifecycle of published package versions across NuGet feeds.
    /// </summary>
    /// <param name="packageId">The unique package identifier.</param>
    /// <param name="version">The specific package version to administer.</param>
    /// <param name="action">Lifecycle action: 'unlist' or 'relist'.</param>
    /// <param name="dryRun">When true, inspects feed state without modifying it.</param>
    /// <param name="feedUrl">Target NuGet v3 feed.</param>
    /// <param name="apiKey">Explicit API key or PAT.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="CallToolResult"/> detailing the administration outcome.</returns>
    [McpServerTool(Name = "nuget_manage_version", Destructive = true)]
    [Description(
        "Administer the visibility and listing lifecycle of published package versions across nuget.org and compatible feeds."
    )]
    public async Task<CallToolResult> nuget_manage_version(
        [Description("The package identifier.")] string packageId,
        [Description("The specific package version to administer.")] string version,
        [Description("Lifecycle action: 'unlist' or 'relist'.")] string action,
        [Description("When true, inspects feed state without modifying it.")] bool dryRun = false,
        [Description("Target NuGet v3 feed.")] string? feedUrl = null,
        [Description("Explicit API key or PAT.")] string? apiKey = null,
        CancellationToken ct = default
    )
    {
        if (
            !Enum.TryParse<ManageVersionAction>(
                action,
                ignoreCase: true,
                out ManageVersionAction parsedAction
            )
        )
        {
            return ToolResultHelper.CreateErrorResult(
                $"Invalid action '{action}'. Valid actions are 'unlist' or 'relist'."
            );
        }

        try
        {
            ManageVersionRequest request = new(packageId, version, parsedAction)
            {
                DryRun = dryRun,
                FeedUrl = feedUrl,
                ApiKey = apiKey,
            };

            ManageVersionResponse response = await _adminService.ManageVersionAsync(request, ct);
            return ToolResultHelper.CreateResult(response.Success, response, apiKey);
        }
        catch (Exception ex)
        {
            return ToolResultHelper.CreateErrorResult(ex, apiKey);
        }
    }

    /// <summary>
    /// Generates the direct nuget.org web portal URL with pre-filled guidance parameters for deprecation.
    /// </summary>
    /// <param name="packageId">The unique package identifier.</param>
    /// <param name="version">The optional package version string.</param>
    /// <param name="reason">Optional deprecation reason (e.g. 'Legacy', 'CriticalBugs', 'Other').</param>
    /// <param name="alternatePackageId">Optional recommended replacement package identifier.</param>
    /// <param name="message">Optional advisory notice message for package consumers.</param>
    /// <returns>A <see cref="CallToolResult"/> containing the deprecation portal URL and guidance.</returns>
    [McpServerTool(Name = "nuget_get_deprecation_url", ReadOnly = true)]
    [Description(
        "Generate the direct nuget.org web portal URL with pre-filled guidance parameters for deprecation."
    )]
    public CallToolResult nuget_get_deprecation_url(
        [Description("The unique package identifier.")] string packageId,
        [Description("The optional package version string.")] string? version = null,
        [Description("Optional deprecation reason (e.g. 'Legacy', 'CriticalBugs', 'Other').")]
            string? reason = null,
        [Description("Optional recommended replacement package identifier.")]
            string? alternatePackageId = null,
        [Description("Optional advisory notice message for package consumers.")]
            string? message = null
    )
    {
        try
        {
            DeprecationUrlRequest request = new(packageId, version)
            {
                Reason = reason,
                AlternatePackageId = alternatePackageId,
                Message = message,
            };

            string url = _adminService.GetDeprecationPortalUrl(request);
            return ToolResultHelper.CreateSuccessResult(
                new
                {
                    url,
                    packageId,
                    version,
                    reason,
                    alternatePackageId,
                    message = "Navigate to this URL in your browser to deprecate the package on nuget.org.",
                }
            );
        }
        catch (Exception ex)
        {
            return ToolResultHelper.CreateErrorResult(ex);
        }
    }
}
