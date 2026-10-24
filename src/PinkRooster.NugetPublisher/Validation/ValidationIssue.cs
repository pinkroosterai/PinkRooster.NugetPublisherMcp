namespace PinkRooster.NugetPublisher.Validation;

/// <summary>
/// Specifies the severity level of a package publish validation diagnostic.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Indicates a critical validation failure that blocks package publishing.
    /// </summary>
    Error,

    /// <summary>
    /// Indicates a non-blocking diagnostic or warning condition.
    /// </summary>
    Warning,
}

/// <summary>
/// Defines standard diagnostic codes for package pre-flight validation issues.
/// </summary>
public static class ValidationIssueCodes
{
    /// <summary>
    /// The package version already exists on the target feed and is immutable.
    /// </summary>
    public const string VersionAlreadyExists = "VersionAlreadyExists";

    /// <summary>
    /// The companion symbol package (.snupkg) was expected but not found adjacent to the package.
    /// </summary>
    public const string SymbolPackageNotFound = "SymbolPackageNotFound";

    /// <summary>
    /// The package manifest (.nuspec) is malformed or could not be loaded.
    /// </summary>
    public const string MalformedManifest = "MalformedManifest";

    /// <summary>
    /// The package archive (.nupkg) is corrupt, invalid, or missing required files.
    /// </summary>
    public const string InvalidPackageArchive = "InvalidPackageArchive";

    /// <summary>
    /// The specified package file does not exist on disk.
    /// </summary>
    public const string PackageFileNotFound = "PackageFileNotFound";

    /// <summary>
    /// The package identifier is missing or empty.
    /// </summary>
    public const string MissingPackageId = "MissingPackageId";

    /// <summary>
    /// The package version is missing or empty.
    /// </summary>
    public const string MissingPackageVersion = "MissingPackageVersion";

    /// <summary>
    /// The package version does not comply with SemVer 2.0.0.
    /// </summary>
    public const string InvalidSemVer = "InvalidSemVer";

    /// <summary>
    /// The package license expression or license file is invalid or missing.
    /// </summary>
    public const string InvalidLicense = "InvalidLicense";
}

/// <summary>
/// Represents a diagnostic issue encountered during package pre-flight safety validation.
/// </summary>
public sealed class ValidationIssue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationIssue"/> class.
    /// </summary>
    public ValidationIssue() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationIssue"/> class with the specified values.
    /// </summary>
    /// <param name="severity">The severity of the issue.</param>
    /// <param name="code">The diagnostic code identifying the category or rule violation.</param>
    /// <param name="message">A descriptive explanation of the issue.</param>
    /// <param name="target">The specific target, file, or property that caused the issue, if applicable.</param>
    public ValidationIssue(
        ValidationSeverity severity,
        string code,
        string message,
        string? target = null
    )
    {
        Severity = severity;
        Code = code;
        Message = message;
        Target = target;
    }

    /// <summary>
    /// Gets the severity level of the issue.
    /// </summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>
    /// Gets the unique diagnostic code identifying the category or rule violation.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the descriptive error or warning message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target entity, element, or file path associated with the issue, if any.
    /// </summary>
    public string? Target { get; init; }
}
