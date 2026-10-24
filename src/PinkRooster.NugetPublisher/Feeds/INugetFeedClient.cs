using PinkRooster.NugetPublisher.Feeds.Models;

namespace PinkRooster.NugetPublisher.Feeds;

/// <summary>
/// Defines client operations for interacting with NuGet v3 feeds, including endpoint discovery,
/// metadata retrieval, search, collision checking, package/symbol publishing, and unlist/relist administration.
/// </summary>
public interface INugetFeedClient
{
    /// <summary>
    /// Discovers service endpoints from the feed service index (e.g. index.json).
    /// </summary>
    /// <param name="feedUrl">The feed service index URL, or null to use the default nuget.org index.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="FeedServiceEndpoints"/> instance containing discovered endpoints.</returns>
    Task<FeedServiceEndpoints> GetServiceEndpointsAsync(
        string? feedUrl = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Retrieves aggregate package metadata, version listings, dependency groups, and vulnerabilities from the registration index.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="feedUrl">The feed URL, or null for default feed.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="PackageMetadataResult"/> containing the package details.</returns>
    Task<PackageMetadataResult> GetPackageMetadataAsync(
        string packageId,
        string? feedUrl = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Searches packages on the feed using the SearchQueryService endpoint.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="take">The maximum number of results to return (default 20).</param>
    /// <param name="prerelease">Whether to include pre-release versions.</param>
    /// <param name="feedUrl">The feed URL, or null for default feed.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of matching package search results.</returns>
    Task<IReadOnlyList<PackageSearchResult>> SearchPackagesAsync(
        string query,
        int take = 20,
        bool prerelease = false,
        string? feedUrl = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lists published packages from a NuGet feed matching the specified query criteria, such as owner, author, or keyword.
    /// </summary>
    /// <param name="query">The query parameters controlling owner/author filtering, paging, and pre-release options.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="PublishedPackagesResult"/> containing matching packages and total hit count.</returns>
    Task<PublishedPackagesResult> ListPublishedPackagesAsync(
        PublishedPackagesQuery query,
        CancellationToken ct = default
    );

    /// <summary>
    /// Checks whether a specific package ID and version already exists on the feed.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version to check.</param>
    /// <param name="feedUrl">The feed URL, or null for default feed.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if the version exists on the feed; otherwise, false.</returns>
    Task<bool> DoesVersionExistAsync(
        string packageId,
        string version,
        string? feedUrl = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Pushes a package stream (.nupkg) to the feed's PackagePublish endpoint.
    /// </summary>
    /// <param name="stream">The readable stream containing the .nupkg file bytes.</param>
    /// <param name="feedUrl">The feed URL, or null for default feed.</param>
    /// <param name="apiKey">An explicit API key or secret token, or null to resolve ambient credentials.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="PushResult"/> detailing the outcome of the push operation.</returns>
    Task<PushResult> PushPackageAsync(
        Stream stream,
        string? feedUrl = null,
        string? apiKey = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Pushes a companion symbol package stream (.snupkg) to the feed's SymbolPackagePublish endpoint.
    /// </summary>
    /// <param name="stream">The readable stream containing the .snupkg file bytes.</param>
    /// <param name="symbolFeedUrl">The symbol publish endpoint URL or service index URL, or null to discover from default feed.</param>
    /// <param name="apiKey">An explicit API key or secret token, or null to resolve ambient credentials.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="PushResult"/> detailing the outcome of the push operation.</returns>
    Task<PushResult> PushSymbolPackageAsync(
        Stream stream,
        string? symbolFeedUrl = null,
        string? apiKey = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Unlists a package version by sending an HTTP DELETE request to the PackagePublish endpoint.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version to unlist.</param>
    /// <param name="feedUrl">The feed URL, or null for default feed.</param>
    /// <param name="apiKey">An explicit API key or secret token, or null to resolve ambient credentials.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The raw <see cref="HttpResponseMessage"/> returned by the feed.</returns>
    Task<HttpResponseMessage> UnlistPackageAsync(
        string packageId,
        string version,
        string? feedUrl = null,
        string? apiKey = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Relists an unlisted package version by sending an HTTP POST request to the PackagePublish endpoint.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version to relist.</param>
    /// <param name="feedUrl">The feed URL, or null for default feed.</param>
    /// <param name="apiKey">An explicit API key or secret token, or null to resolve ambient credentials.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The raw <see cref="HttpResponseMessage"/> returned by the feed.</returns>
    Task<HttpResponseMessage> RelistPackageAsync(
        string packageId,
        string version,
        string? feedUrl = null,
        string? apiKey = null,
        CancellationToken ct = default
    );
}
