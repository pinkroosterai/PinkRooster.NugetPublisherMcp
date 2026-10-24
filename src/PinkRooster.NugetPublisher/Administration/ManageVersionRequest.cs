namespace PinkRooster.NugetPublisher.Administration;

/// <summary>
/// Represents the parameters for administering the lifecycle visibility of a published package version.
/// </summary>
public sealed class ManageVersionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ManageVersionRequest"/> class.
    /// </summary>
    public ManageVersionRequest() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ManageVersionRequest"/> class with the specified package identity and action.
    /// </summary>
    /// <param name="packageId">The unique package identifier.</param>
    /// <param name="version">The specific package version string.</param>
    /// <param name="action">The lifecycle action to perform, defaulting to <see cref="ManageVersionAction.Unlist"/>.</param>
    public ManageVersionRequest(
        string packageId,
        string version,
        ManageVersionAction action = ManageVersionAction.Unlist
    )
    {
        PackageId = packageId;
        Version = version;
        Action = action;
    }

    /// <summary>
    /// Gets or sets the unique package identifier.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the specific package version string.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lifecycle action to execute (<see cref="ManageVersionAction.Unlist"/> or <see cref="ManageVersionAction.Relist"/>).
    /// </summary>
    public ManageVersionAction Action { get; set; } = ManageVersionAction.Unlist;

    /// <summary>
    /// Gets or sets a value indicating whether to perform a dry-run preview check without modifying remote feed state.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets the target NuGet feed URL, or null to target default nuget.org.
    /// </summary>
    public string? FeedUrl { get; set; }

    /// <summary>
    /// Gets or sets an explicit API key or secret token, or null to resolve credentials ambiently.
    /// </summary>
    public string? ApiKey { get; set; }
}
