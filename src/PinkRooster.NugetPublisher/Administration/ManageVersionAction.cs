namespace PinkRooster.NugetPublisher.Administration;

/// <summary>
/// Specifies the lifecycle administration action to perform on a published NuGet package version.
/// </summary>
public enum ManageVersionAction
{
    /// <summary>
    /// Unlist the package version, hiding it from search and browse listings while preserving package restore.
    /// </summary>
    Unlist,

    /// <summary>
    /// Relist a previously unlisted package version, restoring its visibility in search and browse listings.
    /// </summary>
    Relist,
}
