namespace PinkRooster.NugetPublisher.Feeds;

/// <summary>
/// Represents discovered NuGet v3 feed service endpoints extracted from a service index.
/// </summary>
public sealed class FeedServiceEndpoints
{
    /// <summary>
    /// Gets the service index URL from which endpoints were discovered.
    /// </summary>
    public string ServiceIndexUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the package publish endpoint URL (PackagePublish/2.0.0).
    /// </summary>
    public string? PackagePublish { get; init; }

    /// <summary>
    /// Gets the symbol package publish endpoint URL (SymbolPackagePublish/4.9.0).
    /// </summary>
    public string? SymbolPackagePublish { get; init; }

    /// <summary>
    /// Gets the package registration base URL (RegistrationsBaseUrl/3.6.0 or RegistrationsBaseUrl).
    /// </summary>
    public string? RegistrationsBaseUrl { get; init; }

    /// <summary>
    /// Gets the search query service endpoint URL (SearchQueryService).
    /// </summary>
    public string? SearchQueryService { get; init; }

    /// <summary>
    /// Gets the package flat container base address (PackageBaseAddress/3.0.0).
    /// </summary>
    public string? PackageBaseAddress { get; init; }

    /// <summary>
    /// Gets the vulnerability information resource URL (VulnerabilityInfo/6.7.0), if available.
    /// </summary>
    public string? VulnerabilityInfo { get; init; }
}
