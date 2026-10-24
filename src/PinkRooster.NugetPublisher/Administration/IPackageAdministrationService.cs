namespace PinkRooster.NugetPublisher.Administration;

/// <summary>
/// Provides operations for managing package version lifecycle visibility (unlisting, relisting)
/// and generating deprecation portal guidance for NuGet feeds.
/// </summary>
public interface IPackageAdministrationService
{
    /// <summary>
    /// Administers the lifecycle visibility of a published package version, either as a mutating request or preview dry-run.
    /// </summary>
    /// <param name="request">The package management request containing package ID, version, action, and credentials.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="ManageVersionResponse"/> detailing the outcome and updated listing status.</returns>
    Task<ManageVersionResponse> ManageVersionAsync(
        ManageVersionRequest request,
        CancellationToken ct = default
    );

    /// <summary>
    /// Constructs a direct web portal URL for managing package or version deprecation on nuget.org with optional guidance parameters.
    /// </summary>
    /// <param name="request">The deprecation portal URL parameters.</param>
    /// <returns>A fully-qualified URL string pointing to the nuget.org deprecation portal.</returns>
    string GetDeprecationPortalUrl(DeprecationUrlRequest request);
}
