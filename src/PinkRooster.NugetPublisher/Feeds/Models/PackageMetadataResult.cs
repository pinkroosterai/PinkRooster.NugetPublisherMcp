namespace PinkRooster.NugetPublisher.Feeds.Models;

/// <summary>
/// Represents the aggregate package metadata and version details retrieved from a NuGet feed registration index.
/// </summary>
public sealed class PackageMetadataResult
{
    /// <summary>
    /// Gets the package identifier.
    /// </summary>
    public string PackageId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the latest published version string, or empty if no versions exist.
    /// </summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the authors declared for the latest package version.
    /// </summary>
    public string Authors { get; init; } = string.Empty;

    /// <summary>
    /// Gets the description extracted from the latest package version.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SPDX license expression declared for the latest package version, if specified.
    /// </summary>
    public string? LicenseExpression { get; init; }

    /// <summary>
    /// Gets the license URL declared for the latest package version, if specified.
    /// </summary>
    public string? LicenseUrl { get; init; }

    /// <summary>
    /// Gets the project URL declared for the latest package version, if specified.
    /// </summary>
    public string? ProjectUrl { get; init; }

    /// <summary>
    /// Gets the tags associated with the latest package version.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the list of all published version strings for this package.
    /// </summary>
    public IReadOnlyList<string> Versions { get; init; } = [];

    /// <summary>
    /// Gets detailed metadata for each published version of this package.
    /// </summary>
    public IReadOnlyList<PackageVersionMetadata> VersionDetails { get; init; } = [];

    /// <summary>
    /// Gets the dependency groups declared for the latest package version.
    /// </summary>
    public IReadOnlyList<PackageDependencyGroupInfo> DependencyGroups { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the package was found on the target feed.
    /// </summary>
    public bool Exists { get; init; }
}
