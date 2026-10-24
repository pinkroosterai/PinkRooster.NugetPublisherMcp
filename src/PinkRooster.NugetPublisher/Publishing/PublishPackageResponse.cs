namespace PinkRooster.NugetPublisher.Publishing;

/// <summary>
/// Represents the result of publishing a NuGet package and its companion symbols.
/// </summary>
public sealed class PublishPackageResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the publish operation completed successfully or was skipped as an intentional duplicate.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the package identifier extracted from the package archive.
    /// </summary>
    public string? PackageId { get; set; }

    /// <summary>
    /// Gets or sets the package version extracted from the package archive.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the target feed URL to which publication was directed.
    /// </summary>
    public string FeedUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the primary .nupkg package was successfully pushed to the feed.
    /// </summary>
    public bool PublishedPackage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the companion .snupkg symbol package was successfully pushed.
    /// </summary>
    public bool PublishedSymbols { get; set; }

    /// <summary>
    /// Gets or sets the file path to the companion symbol package, if detected on disk.
    /// </summary>
    public string? SymbolPackagePath { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code returned by the target feed or diagnostic result code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets non-fatal diagnostic warnings generated during validation or publishing.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; set; } = [];

    /// <summary>
    /// Gets or sets a descriptive outcome or diagnostic message, sanitized of any secrets.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
