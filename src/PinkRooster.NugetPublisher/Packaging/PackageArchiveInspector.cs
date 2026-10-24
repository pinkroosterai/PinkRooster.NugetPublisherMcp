using System.IO.Compression;
using System.Xml.Linq;
using NuGet.Packaging.Licenses;
using NuGet.Versioning;

namespace PinkRooster.NugetPublisher.Packaging;

/// <summary>
/// Provides inspection and validation services for local NuGet package archives (.nupkg).
/// </summary>
public static class PackageArchiveInspector
{
    private static readonly char[] TagSeparators = [' ', ',', ';'];

    /// <summary>
    /// Inspects a local NuGet package archive (.nupkg), extracting its manifest, files, dependencies,
    /// and performing SemVer 2.0.0 and SPDX license validation.
    /// </summary>
    /// <param name="nupkgPath">The path to the .nupkg file to inspect.</param>
    /// <returns>A <see cref="PackageArchiveInfo"/> containing extracted metadata and validation results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nupkgPath"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nupkgPath"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified .nupkg file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the package archive is corrupt or lacks a valid .nuspec manifest.</exception>
    public static PackageArchiveInfo Inspect(string nupkgPath)
    {
        ArgumentNullException.ThrowIfNull(nupkgPath);

        if (string.IsNullOrWhiteSpace(nupkgPath))
        {
            throw new ArgumentException("Package path cannot be empty or whitespace.", nameof(nupkgPath));
        }

        if (!File.Exists(nupkgPath))
        {
            throw new FileNotFoundException($"Package file not found: {nupkgPath}", nupkgPath);
        }

        string fullPath = Path.GetFullPath(nupkgPath);
        List<string> files = [];

        using FileStream fileStream = File.OpenRead(fullPath);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Read);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            files.Add(entry.FullName);
        }

        ZipArchiveEntry? nuspecEntry = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !entry.FullName.Contains('/'));

        if (nuspecEntry is null)
        {
            nuspecEntry = archive.Entries.FirstOrDefault(entry =>
                entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        }

        if (nuspecEntry is null)
        {
            throw new InvalidOperationException($"Package archive '{nupkgPath}' does not contain a .nuspec manifest.");
        }

        using Stream nuspecStream = nuspecEntry.Open();
        XDocument doc = XDocument.Load(nuspecStream);

        XElement? metadata = doc.Descendants().FirstOrDefault(elem =>
            string.Equals(elem.Name.LocalName, "metadata", StringComparison.OrdinalIgnoreCase));

        if (metadata is null)
        {
            throw new InvalidOperationException($"The .nuspec manifest in '{nupkgPath}' does not contain a <metadata> element.");
        }

        string packageId = GetElementValue(metadata, "id");
        string version = GetElementValue(metadata, "version");
        string authors = GetElementValue(metadata, "authors");
        string description = GetElementValue(metadata, "description");
        string? projectUrl = GetOptionalElementValue(metadata, "projectUrl");

        string? rawTags = GetOptionalElementValue(metadata, "tags");
        List<string> tags = string.IsNullOrWhiteSpace(rawTags)
            ? []
            : [.. rawTags.Split(TagSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        (string? licenseExpression, string? licenseFile) = ExtractLicense(metadata);

        (List<PackageDependencyInfo> dependencies, List<string> declaredFrameworks) = ExtractDependencies(metadata);

        List<string> targetFrameworks = [.. declaredFrameworks];
        foreach (string file in files)
        {
            string[] segments = file.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 &&
                (string.Equals(segments[0], "lib", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(segments[0], "ref", StringComparison.OrdinalIgnoreCase)))
            {
                targetFrameworks.Add(segments[1]);
            }
        }

        List<string> distinctTargetFrameworks = targetFrameworks
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? packageDir = Path.GetDirectoryName(fullPath);
        bool hasCompanionSymbols = false;
        string? companionSymbolsPath = null;

        if (!string.IsNullOrEmpty(packageDir) && !string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(version))
        {
            string snupkgCandidate = Path.Combine(packageDir, $"{packageId}.{version}.snupkg");
            if (File.Exists(snupkgCandidate))
            {
                hasCompanionSymbols = true;
                companionSymbolsPath = Path.GetFullPath(snupkgCandidate);
            }
        }

        List<string> validationErrors = [];

        if (string.IsNullOrWhiteSpace(packageId))
        {
            validationErrors.Add("Package ID is missing or empty.");
        }

        bool isValidSemVer = ValidateSemVer(version, validationErrors);
        bool isValidLicense = ValidateLicense(licenseExpression, licenseFile, files, validationErrors);

        bool isValid = validationErrors.Count == 0;

        return new PackageArchiveInfo
        {
            PackagePath = fullPath,
            PackageId = packageId,
            Version = version,
            Authors = authors,
            Description = description,
            LicenseExpression = licenseExpression,
            LicenseFile = licenseFile,
            ProjectUrl = projectUrl,
            Tags = tags,
            TargetFrameworks = distinctTargetFrameworks,
            Dependencies = dependencies,
            Files = files,
            HasCompanionSymbols = hasCompanionSymbols,
            CompanionSymbolsPath = companionSymbolsPath,
            IsValidSemVer = isValidSemVer,
            IsValidLicenseExpression = isValidLicense,
            IsValid = isValid,
            ValidationErrors = validationErrors
        };
    }

    private static string GetElementValue(XElement parent, string localName)
    {
        XElement? elem = parent.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        return elem?.Value.Trim() ?? string.Empty;
    }

    private static string? GetOptionalElementValue(XElement parent, string localName)
    {
        XElement? elem = parent.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        string? val = elem?.Value.Trim();
        return string.IsNullOrEmpty(val) ? null : val;
    }

    private static (string? Expression, string? File) ExtractLicense(XElement metadata)
    {
        XElement? licenseElem = metadata.Elements().FirstOrDefault(elem =>
            string.Equals(elem.Name.LocalName, "license", StringComparison.OrdinalIgnoreCase));

        if (licenseElem is null)
        {
            return (null, null);
        }

        string? typeAttr = licenseElem.Attribute("type")?.Value.Trim();
        string value = licenseElem.Value.Trim();

        if (string.Equals(typeAttr, "expression", StringComparison.OrdinalIgnoreCase))
        {
            return (value, null);
        }

        if (string.Equals(typeAttr, "file", StringComparison.OrdinalIgnoreCase))
        {
            return (null, value);
        }

        if (value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            value.Contains('/') ||
            value.Contains('\\'))
        {
            return (null, value);
        }

        return (value, null);
    }

    private static (List<PackageDependencyInfo> Dependencies, List<string> Frameworks) ExtractDependencies(XElement metadata)
    {
        List<PackageDependencyInfo> dependencies = [];
        List<string> frameworks = [];

        XElement? dependenciesElem = metadata.Elements().FirstOrDefault(elem =>
            string.Equals(elem.Name.LocalName, "dependencies", StringComparison.OrdinalIgnoreCase));

        if (dependenciesElem is null)
        {
            return (dependencies, frameworks);
        }

        foreach (XElement group in dependenciesElem.Elements().Where(elem =>
            string.Equals(elem.Name.LocalName, "group", StringComparison.OrdinalIgnoreCase)))
        {
            string? tfm = group.Attribute("targetFramework")?.Value.Trim();
            if (!string.IsNullOrEmpty(tfm))
            {
                frameworks.Add(tfm);
            }

            foreach (XElement dep in group.Elements().Where(elem =>
                string.Equals(elem.Name.LocalName, "dependency", StringComparison.OrdinalIgnoreCase)))
            {
                string id = dep.Attribute("id")?.Value.Trim() ?? string.Empty;
                string ver = dep.Attribute("version")?.Value.Trim() ?? string.Empty;
                dependencies.Add(new PackageDependencyInfo(id, ver, tfm));
            }
        }

        foreach (XElement dep in dependenciesElem.Elements().Where(elem =>
            string.Equals(elem.Name.LocalName, "dependency", StringComparison.OrdinalIgnoreCase)))
        {
            string id = dep.Attribute("id")?.Value.Trim() ?? string.Empty;
            string ver = dep.Attribute("version")?.Value.Trim() ?? string.Empty;
            dependencies.Add(new PackageDependencyInfo(id, ver, null));
        }

        return (dependencies, frameworks);
    }

    private static bool ValidateSemVer(string version, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            errors.Add("Package version is missing or empty.");
            return false;
        }

        if (!NuGetVersion.TryParseStrict(version, out NuGetVersion? parsedVersion) || parsedVersion is null)
        {
            errors.Add($"Version '{version}' is not a valid SemVer 2.0.0 string.");
            return false;
        }

        if (parsedVersion.Revision > 0)
        {
            errors.Add($"Version '{version}' contains a 4th revision segment, which is not permitted under SemVer 2.0.0.");
            return false;
        }

        return true;
    }

    private static bool ValidateLicense(
        string? licenseExpression,
        string? licenseFile,
        IReadOnlyList<string> packageFiles,
        List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(licenseExpression) && !string.IsNullOrWhiteSpace(licenseFile))
        {
            errors.Add("Package specifies both a license expression and a license file; these are mutually exclusive.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(licenseExpression))
        {
            try
            {
                NuGetLicenseExpression expr = NuGetLicenseExpression.Parse(licenseExpression);
                if (!expr.HasOnlyStandardIdentifiers())
                {
                    List<string> nonStandardIdentifiers = [];
                    expr.OnEachLeafNode(
                        license =>
                        {
                            if (!license.IsStandardLicense)
                            {
                                nonStandardIdentifiers.Add(license.Identifier);
                            }
                        },
                        exception =>
                        {
                            nonStandardIdentifiers.Add(exception.Identifier);
                        });

                    string identifiers = nonStandardIdentifiers.Count > 0
                        ? string.Join(", ", nonStandardIdentifiers)
                        : licenseExpression;

                    errors.Add($"License expression '{licenseExpression}' contains non-standard SPDX identifier(s): {identifiers}.");
                    return false;
                }

                return true;
            }
            catch (NuGetLicenseExpressionParsingException ex)
            {
                errors.Add($"License expression '{licenseExpression}' is malformed: {ex.Message}");
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(licenseFile))
        {
            string normalizedTarget = licenseFile.Replace('\\', '/').TrimStart('/');
            bool fileFound = packageFiles.Any(f =>
                string.Equals(f.Replace('\\', '/').TrimStart('/'), normalizedTarget, StringComparison.OrdinalIgnoreCase));

            if (!fileFound)
            {
                errors.Add($"License file '{licenseFile}' was specified in the manifest but not found in the package archive.");
                return false;
            }

            return true;
        }

        errors.Add("Package does not specify a license expression or license file.");
        return false;
    }
}
