namespace PinkRooster.NugetPublisher.Administration;

/// <summary>
/// Represents parameters for constructing a direct portal deprecation link for nuget.org.
/// </summary>
public sealed class DeprecationUrlRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeprecationUrlRequest"/> class.
    /// </summary>
    public DeprecationUrlRequest() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeprecationUrlRequest"/> class with the specified package identifier and optional version.
    /// </summary>
    /// <param name="packageId">The unique package identifier.</param>
    /// <param name="version">The optional package version string.</param>
    public DeprecationUrlRequest(string packageId, string? version = null)
    {
        PackageId = packageId;
        Version = version;
    }

    /// <summary>
    /// Gets or sets the unique package identifier.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional package version string to target a specific version deprecation.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets an optional deprecation reason (e.g. "Legacy", "CriticalBugs", "Other").
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets an optional recommended replacement package identifier.
    /// </summary>
    public string? AlternatePackageId { get; set; }

    /// <summary>
    /// Gets or sets an optional advisory or deprecation notice message for package consumers.
    /// </summary>
    public string? Message { get; set; }
}
