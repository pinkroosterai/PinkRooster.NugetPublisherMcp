namespace PinkRooster.NugetPublisher.Packaging;

/// <summary>
/// Represents a dependency declared in a package manifest.
/// </summary>
public sealed class PackageDependencyInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PackageDependencyInfo"/> class.
    /// </summary>
    /// <param name="id">The package identifier of the dependency.</param>
    /// <param name="versionRange">The version range specification.</param>
    /// <param name="targetFramework">The target framework group for the dependency, if specified.</param>
    public PackageDependencyInfo(string id, string versionRange, string? targetFramework = null)
    {
        Id = id;
        VersionRange = versionRange;
        TargetFramework = targetFramework;
    }

    /// <summary>
    /// Gets the identifier of the dependent package.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the version range specification of the dependency.
    /// </summary>
    public string VersionRange { get; init; }

    /// <summary>
    /// Gets the target framework for which this dependency applies, if scoped to a framework group.
    /// </summary>
    public string? TargetFramework { get; init; }
}

/// <summary>
/// Contains metadata, file listings, dependencies, and validation diagnostics extracted from a local NuGet package archive.
/// </summary>
public sealed class PackageArchiveInfo
{
    /// <summary>
    /// Gets the file path to the inspected package archive.
    /// </summary>
    public string PackagePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the package identifier (ID) extracted from the manifest.
    /// </summary>
    public string PackageId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the package version string extracted from the manifest.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the authors declared in the package manifest.
    /// </summary>
    public string Authors { get; init; } = string.Empty;

    /// <summary>
    /// Gets the description of the package extracted from the manifest.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SPDX license expression declared in the package manifest, if specified.
    /// </summary>
    public string? LicenseExpression { get; init; }

    /// <summary>
    /// Gets the relative path to the packaged license file, if specified.
    /// </summary>
    public string? LicenseFile { get; init; }

    /// <summary>
    /// Gets the project URL declared in the package manifest, if specified.
    /// </summary>
    public string? ProjectUrl { get; init; }

    /// <summary>
    /// Gets the tags associated with the package.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the target frameworks supported by the package (from assemblies and dependency groups).
    /// </summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    /// <summary>
    /// Gets the declared package dependencies across all target frameworks.
    /// </summary>
    public IReadOnlyList<PackageDependencyInfo> Dependencies { get; init; } = [];

    /// <summary>
    /// Gets the complete list of file paths contained in the package archive.
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether a companion symbol package (.snupkg) exists in the package directory.
    /// </summary>
    public bool HasCompanionSymbols { get; init; }

    /// <summary>
    /// Gets the full path to the companion symbol package, if detected.
    /// </summary>
    public string? CompanionSymbolsPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the package version is a valid SemVer 2.0.0 format.
    /// </summary>
    public bool IsValidSemVer { get; init; }

    /// <summary>
    /// Gets a value indicating whether the license specification (SPDX expression or file) is valid.
    /// </summary>
    public bool IsValidLicenseExpression { get; init; }

    /// <summary>
    /// Gets a value indicating whether all package validations passed without errors.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the list of validation errors encountered during archive inspection.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
}
