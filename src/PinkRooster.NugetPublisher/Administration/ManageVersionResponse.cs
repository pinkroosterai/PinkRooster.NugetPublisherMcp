namespace PinkRooster.NugetPublisher.Administration;

/// <summary>
/// Represents the outcome of administering the lifecycle visibility of a published package version.
/// </summary>
public sealed class ManageVersionResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the administration operation or dry-run inspection succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the unique package identifier.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the package version string.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lifecycle action performed (e.g. "unlist" or "relist").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the operation was executed as a non-mutating dry-run preview.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets the listing status of the version prior to the operation (e.g. "Listed", "Unlisted", or null if unknown/failed).
    /// </summary>
    public string? PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets the current listing status of the version after the operation (e.g. "Listed", "Unlisted", or null if unknown/failed).
    /// </summary>
    public string? CurrentStatus { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code returned by the feed or diagnostic outcome code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets a descriptive human-readable outcome message, sanitized of any secret tokens.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
