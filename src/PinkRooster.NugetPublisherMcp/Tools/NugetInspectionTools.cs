using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PinkRooster.NugetPublisher.Packaging;
using PinkRooster.NugetPublisher.Validation;

namespace PinkRooster.NugetPublisherMcp.Tools;

/// <summary>
/// Provides MCP tools for local package inspection and pre-flight validation.
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
public sealed class NugetInspectionTools
{
    private readonly PublishSafetyValidator _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="NugetInspectionTools"/> class.
    /// </summary>
    /// <param name="validator">The publish safety validator.</param>
    public NugetInspectionTools(PublishSafetyValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
    }

    /// <summary>
    /// Reads a local .nupkg package archive to inspect its manifest, SemVer, license expressions, and companion symbol package.
    /// </summary>
    /// <param name="nupkgPath">The file path to the .nupkg archive.</param>
    /// <returns>A <see cref="CallToolResult"/> containing the package archive inspection details.</returns>
    [McpServerTool(Name = "nuget_inspect_local", ReadOnly = true)]
    [Description(
        "Read local .nupkg nuspec manifest, validate SemVer, check SPDX license expressions, and verify .snupkg presence without touching a network."
    )]
    public CallToolResult nuget_inspect_local(
        [Description("Absolute or relative file path to the .nupkg archive.")] string nupkgPath
    )
    {
        try
        {
            PackageArchiveInfo info = PackageArchiveInspector.Inspect(nupkgPath);
            return ToolResultHelper.CreateSuccessResult(info);
        }
        catch (Exception ex)
        {
            return ToolResultHelper.CreateErrorResult(ex);
        }
    }

    /// <summary>
    /// Runs a standalone dry-run pre-flight validation check against a target feed without modifying the feed.
    /// </summary>
    /// <param name="nupkgPath">The file path to the .nupkg archive.</param>
    /// <param name="feedUrl">Target feed service index URL, or null for default nuget.org.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="CallToolResult"/> containing the pre-flight validation results.</returns>
    [McpServerTool(Name = "nuget_validate_publish", ReadOnly = true)]
    [Description(
        "Standalone dry-run preview tool executing pre-flight checks against a target feed without publishing."
    )]
    public async Task<CallToolResult> nuget_validate_publish(
        [Description("Absolute or relative file path to the .nupkg archive.")] string nupkgPath,
        [Description("Target feed service index URL, or null for default nuget.org.")]
            string? feedUrl = null,
        CancellationToken ct = default
    )
    {
        try
        {
            ValidationResult result = await _validator.ValidateAsync(nupkgPath, feedUrl, ct);
            return ToolResultHelper.CreateResult(result.IsValid, result);
        }
        catch (Exception ex)
        {
            return ToolResultHelper.CreateErrorResult(ex);
        }
    }
}
