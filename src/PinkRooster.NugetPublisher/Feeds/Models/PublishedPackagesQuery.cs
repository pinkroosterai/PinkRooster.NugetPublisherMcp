namespace PinkRooster.NugetPublisher.Feeds.Models;

/// <summary>
/// Represents criteria for listing published packages from a NuGet feed, filtered by owner, author, or search query.
/// </summary>
public sealed class PublishedPackagesQuery
{
    /// <summary>
    /// Gets the package owner username filter (e.g. "PinkRoosterAi").
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// Gets the package author filter (e.g. "PinkRooster").
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the optional search query or keyword filter within published packages.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Gets the target feed service index URL, or null to use the default feed.
    /// </summary>
    public string? FeedUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include pre-release package versions. Defaults to <c>true</c>.
    /// </summary>
    public bool Prerelease { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of packages to return. Defaults to 20.
    /// </summary>
    public int Take { get; init; } = 20;

    /// <summary>
    /// Gets the number of packages to skip for pagination. Defaults to 0.
    /// </summary>
    public int Skip { get; init; }
}
