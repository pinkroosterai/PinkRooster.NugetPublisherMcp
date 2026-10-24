namespace PinkRooster.NugetPublisher.Feeds.Models;

/// <summary>
/// Represents a package search hit returned by the NuGet SearchQueryService.
/// </summary>
public sealed class PackageSearchResult
{
    /// <summary>
    /// Gets the package identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the latest matching version of the package.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the package description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of package authors.
    /// </summary>
    public IReadOnlyList<string> Authors { get; init; } = [];

    /// <summary>
    /// Gets the list of package owners.
    /// </summary>
    public IReadOnlyList<string> Owners { get; init; } = [];

    /// <summary>
    /// Gets the list of tags associated with the package.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the project URL, if specified.
    /// </summary>
    public string? ProjectUrl { get; init; }

    /// <summary>
    /// Gets the total download count for the package across all versions, if available.
    /// </summary>
    public long? TotalDownloads { get; init; }

    /// <summary>
    /// Gets a value indicating whether the package author or ID is verified, if provided.
    /// </summary>
    public bool? Verified { get; init; }

    /// <summary>
    /// Gets the list of versions returned for this search entry.
    /// </summary>
    public IReadOnlyList<string> Versions { get; init; } = [];
}
