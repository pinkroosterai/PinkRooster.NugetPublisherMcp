namespace PinkRooster.NugetPublisher.Feeds.Models;

/// <summary>
/// Represents package deprecation information from feed metadata.
/// </summary>
public sealed class PackageDeprecationMetadata
{
    /// <summary>
    /// Gets the list of deprecation reasons (e.g. Legacy, CriticalBugs, Other).
    /// </summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];

    /// <summary>
    /// Gets the human-readable message explaining the deprecation, if specified.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets the suggested alternate package identifier, if specified.
    /// </summary>
    public string? AlternatePackageId { get; init; }
}
