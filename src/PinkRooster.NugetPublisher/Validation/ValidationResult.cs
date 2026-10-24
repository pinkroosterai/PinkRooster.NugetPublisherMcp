using PinkRooster.NugetPublisher.Packaging;

namespace PinkRooster.NugetPublisher.Validation;

/// <summary>
/// Represents the result of a pre-flight publish safety validation check for a NuGet package.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation passed without any error-severity issues.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the package identifier extracted from the package archive, if available.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Gets the package version string extracted from the package archive, if available.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets a value indicating whether the package version already exists on the target feed.
    /// </summary>
    public bool HasCollision { get; init; }

    /// <summary>
    /// Gets the detailed package archive inspection information, if archive inspection succeeded.
    /// </summary>
    public PackageArchiveInfo? ArchiveInfo { get; init; }

    /// <summary>
    /// Gets the list of validation issues encountered, including errors and warnings.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
}
