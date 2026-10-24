namespace PinkRooster.NugetPublisher.Feeds.Models;

/// <summary>
/// Represents metadata for a specific published package version.
/// </summary>
public sealed class PackageVersionMetadata
{
    /// <summary>
    /// Gets the package version string.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this version is listed in the feed.
    /// </summary>
    public bool Listed { get; init; } = true;

    /// <summary>
    /// Gets the date and time when the version was published, if available.
    /// </summary>
    public DateTimeOffset? Published { get; init; }

    /// <summary>
    /// Gets the description for this version.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the authors for this version.
    /// </summary>
    public string? Authors { get; init; }

    /// <summary>
    /// Gets the SPDX license expression for this version, if specified.
    /// </summary>
    public string? LicenseExpression { get; init; }

    /// <summary>
    /// Gets the license URL for this version, if specified.
    /// </summary>
    public string? LicenseUrl { get; init; }

    /// <summary>
    /// Gets the summary description for this version, if specified.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Gets the project URL for this version, if specified.
    /// </summary>
    public string? ProjectUrl { get; init; }

    /// <summary>
    /// Gets the package content URL (.nupkg download link), if available.
    /// </summary>
    public string? PackageContent { get; init; }

    /// <summary>
    /// Gets the tags associated with this version.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the dependency groups declared for this version.
    /// </summary>
    public IReadOnlyList<PackageDependencyGroupInfo> DependencyGroups { get; init; } = [];

    /// <summary>
    /// Gets the vulnerabilities reported for this version.
    /// </summary>
    public IReadOnlyList<PackageVulnerabilityMetadata> Vulnerabilities { get; init; } = [];
}
