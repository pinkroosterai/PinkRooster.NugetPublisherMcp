using PinkRooster.NugetPublisher.Packaging;

namespace PinkRooster.NugetPublisher.Feeds.Models;

/// <summary>
/// Represents a group of package dependencies scoped to a specific target framework.
/// </summary>
public sealed class PackageDependencyGroupInfo
{
    /// <summary>
    /// Gets the target framework moniker for this dependency group, or null if framework-agnostic.
    /// </summary>
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Gets the list of package dependencies in this group.
    /// </summary>
    public IReadOnlyList<PackageDependencyInfo> Dependencies { get; init; } = [];
}
