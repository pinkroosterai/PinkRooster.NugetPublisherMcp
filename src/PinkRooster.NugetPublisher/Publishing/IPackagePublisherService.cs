namespace PinkRooster.NugetPublisher.Publishing;

/// <summary>
/// Defines operations for publishing NuGet packages (.nupkg) and companion symbol packages (.snupkg) to NuGet feeds.
/// </summary>
public interface IPackagePublisherService
{
    /// <summary>
    /// Publishes a NuGet package and optional companion symbols to the target feed with pre-flight safety validations.
    /// </summary>
    /// <param name="request">The publishing request parameters.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="PublishPackageResponse"/> detailing the outcome of the publish operation.</returns>
    Task<PublishPackageResponse> PublishAsync(
        PublishPackageRequest request,
        CancellationToken ct = default
    );
}
