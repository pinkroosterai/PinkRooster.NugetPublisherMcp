using System.Xml;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Packaging;

namespace PinkRooster.NugetPublisher.Validation;

/// <summary>
/// Provides pre-flight safety and dry-run validation for NuGet packages prior to publication.
/// Enforces manifest validity, SemVer compliance, SPDX licensing, companion symbol detection,
/// and remote feed version immutability collision checks.
/// </summary>
public sealed class PublishSafetyValidator
{
    private const string DefaultFeedUrl = "https://api.nuget.org/v3/index.json";
    private readonly INugetFeedClient _feedClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishSafetyValidator"/> class with the specified feed client.
    /// </summary>
    /// <param name="feedClient">The client used to query target feeds for package existence and service discovery.</param>
    public PublishSafetyValidator(INugetFeedClient feedClient)
    {
        ArgumentNullException.ThrowIfNull(feedClient);
        _feedClient = feedClient;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishSafetyValidator"/> class with a default feed client.
    /// </summary>
    public PublishSafetyValidator()
        : this(new NugetFeedClient()) { }

    /// <summary>
    /// Validates a local package archive against remote feed version collision rules and checks companion symbol presence.
    /// </summary>
    /// <param name="nupkgPath">The path to the local .nupkg file.</param>
    /// <param name="feedUrl">The feed service index URL, or null to query the default nuget.org feed.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="ValidationResult"/> detailing validation status, issues, and feed collision status.</returns>
    public Task<ValidationResult> ValidateAsync(
        string nupkgPath,
        string? feedUrl = null,
        CancellationToken ct = default
    )
    {
        return ValidateAsync(nupkgPath, feedUrl, expectSymbols: false, ct);
    }

    /// <summary>
    /// Validates a local package archive against remote feed version collision rules using the default feed.
    /// </summary>
    /// <param name="nupkgPath">The path to the local .nupkg file.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="ValidationResult"/> detailing validation status, issues, and feed collision status.</returns>
    public Task<ValidationResult> ValidateAsync(string nupkgPath, CancellationToken ct)
    {
        return ValidateAsync(nupkgPath, feedUrl: null, expectSymbols: false, ct);
    }

    /// <summary>
    /// Validates a local package archive against remote feed version collision rules with optional symbol package expectation.
    /// </summary>
    /// <param name="nupkgPath">The path to the local .nupkg file.</param>
    /// <param name="feedUrl">The feed service index URL, or null to query the default nuget.org feed.</param>
    /// <param name="expectSymbols">Whether a companion symbol package (.snupkg) is expected adjacent to the .nupkg file.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="ValidationResult"/> detailing validation status, issues, and feed collision status.</returns>
    public async Task<ValidationResult> ValidateAsync(
        string nupkgPath,
        string? feedUrl,
        bool expectSymbols,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(nupkgPath);

        if (string.IsNullOrWhiteSpace(nupkgPath))
        {
            throw new ArgumentException(
                "Package path cannot be empty or whitespace.",
                nameof(nupkgPath)
            );
        }

        // 1. Validates local package archive: calls PackageArchiveInspector.Inspect
        PackageArchiveInfo archiveInfo;
        try
        {
            archiveInfo = PackageArchiveInspector.Inspect(nupkgPath);
        }
        catch (FileNotFoundException ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                PackageId = null,
                Version = null,
                HasCollision = false,
                ArchiveInfo = null,
                Issues =
                [
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationIssueCodes.PackageFileNotFound,
                        ex.Message,
                        nupkgPath
                    ),
                ],
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            string code =
                ex is InvalidOperationException || ex is XmlException
                    ? ValidationIssueCodes.MalformedManifest
                    : ValidationIssueCodes.InvalidPackageArchive;

            return new ValidationResult
            {
                IsValid = false,
                PackageId = null,
                Version = null,
                HasCollision = false,
                ArchiveInfo = null,
                Issues =
                [
                    new ValidationIssue(ValidationSeverity.Error, code, ex.Message, nupkgPath),
                ],
            };
        }

        List<ValidationIssue> issues = [];

        // Record archive validation errors (missing metadata, invalid SemVer, invalid SPDX expression)
        foreach (string error in archiveInfo.ValidationErrors)
        {
            string code = ClassifyValidationError(error);
            issues.Add(
                new ValidationIssue(ValidationSeverity.Error, code, error, archiveInfo.PackagePath)
            );
        }

        if (!archiveInfo.IsValid && issues.Count == 0)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    ValidationIssueCodes.InvalidPackageArchive,
                    "Package archive validation failed.",
                    archiveInfo.PackagePath
                )
            );
        }

        // 2. Checks companion symbol package: if .snupkg is missing when symbols are expected, records a warning issue
        if (expectSymbols && !archiveInfo.HasCompanionSymbols)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Warning,
                    ValidationIssueCodes.SymbolPackageNotFound,
                    $"Companion symbol package (.snupkg) was not found for package '{archiveInfo.PackageId}' version '{archiveInfo.Version}'.",
                    archiveInfo.PackagePath
                )
            );
        }

        // 3. Remote feed collision check (Review Focus #1)
        bool hasCollision = false;
        if (
            !string.IsNullOrWhiteSpace(archiveInfo.PackageId)
            && !string.IsNullOrWhiteSpace(archiveInfo.Version)
        )
        {
            try
            {
                bool exists = await _feedClient.DoesVersionExistAsync(
                    archiveInfo.PackageId,
                    archiveInfo.Version,
                    feedUrl,
                    ct
                );

                if (exists)
                {
                    hasCollision = true;
                    string resolvedFeed = string.IsNullOrWhiteSpace(feedUrl)
                        ? DefaultFeedUrl
                        : feedUrl;

                    issues.Add(
                        new ValidationIssue(
                            ValidationSeverity.Error,
                            ValidationIssueCodes.VersionAlreadyExists,
                            $"Version {archiveInfo.Version} of {archiveInfo.PackageId} is already published on {resolvedFeed} and is immutable.",
                            archiveInfo.PackageId
                        )
                    );
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        ValidationIssueCodes.InvalidPackageArchive,
                        $"Failed to check remote feed for existing version: {ex.Message}",
                        archiveInfo.PackageId
                    )
                );
            }
        }

        // 4. Returns consolidated ValidationResult: IsValid is true if and only if there are 0 Error-severity issues
        bool isValid = issues.All(i => i.Severity != ValidationSeverity.Error);

        return new ValidationResult
        {
            IsValid = isValid,
            PackageId = archiveInfo.PackageId,
            Version = archiveInfo.Version,
            HasCollision = hasCollision,
            ArchiveInfo = archiveInfo,
            Issues = issues,
        };
    }

    private static string ClassifyValidationError(string error)
    {
        if (
            error.Contains("SemVer", StringComparison.OrdinalIgnoreCase)
            || error.Contains("revision", StringComparison.OrdinalIgnoreCase)
        )
        {
            return ValidationIssueCodes.InvalidSemVer;
        }

        if (
            error.Contains("license", StringComparison.OrdinalIgnoreCase)
            || error.Contains("SPDX", StringComparison.OrdinalIgnoreCase)
        )
        {
            return ValidationIssueCodes.InvalidLicense;
        }

        if (error.Contains("Package ID", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationIssueCodes.MissingPackageId;
        }

        if (error.Contains("version is missing", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationIssueCodes.MissingPackageVersion;
        }

        return ValidationIssueCodes.InvalidPackageArchive;
    }
}
