namespace PinkRooster.NugetPublisher.Feeds.Models;

/// <summary>
/// Represents the result of querying published packages from a NuGet feed.
/// </summary>
public sealed class PublishedPackagesResult
{
    /// <summary>
    /// Gets the total number of hits matching the query on the feed.
    /// </summary>
    public int TotalHits { get; init; }

    /// <summary>
    /// Gets the package owner filter applied to the query, if specified.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// Gets the package author filter applied to the query, if specified.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the feed service index URL that was queried.
    /// </summary>
    public string FeedUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of package search results returned for the query.
    /// </summary>
    public IReadOnlyList<PackageSearchResult> Packages { get; init; } = [];
}
