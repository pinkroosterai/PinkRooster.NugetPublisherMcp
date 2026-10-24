namespace PinkRooster.NugetPublisher.Publishing;

/// <summary>
/// Represents the parameters for publishing a NuGet package and optional companion symbol package.
/// </summary>
public sealed class PublishPackageRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PublishPackageRequest"/> class.
    /// </summary>
    public PublishPackageRequest() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishPackageRequest"/> class with the specified package path.
    /// </summary>
    /// <param name="nupkgPath">The file path to the .nupkg package archive to publish.</param>
    public PublishPackageRequest(string nupkgPath)
    {
        NupkgPath = nupkgPath;
    }

    /// <summary>
    /// Gets or sets the file path to the .nupkg package archive to publish.
    /// </summary>
    public string NupkgPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target NuGet feed URL, or null to publish to the default nuget.org feed.
    /// </summary>
    public string? FeedUrl { get; set; }

    /// <summary>
    /// Gets or sets an explicit API key or secret token, or null to resolve credentials from ambient settings.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets an explicit symbol server URL, or null to discover the symbol publish endpoint from the feed.
    /// </summary>
    public string? SymbolFeedUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether version collision conflicts (HTTP 409) should be skipped gracefully.
    /// </summary>
    public bool SkipDuplicate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether non-fatal pre-flight validation errors should be bypassed.
    /// </summary>
    public bool Force { get; set; }
}
