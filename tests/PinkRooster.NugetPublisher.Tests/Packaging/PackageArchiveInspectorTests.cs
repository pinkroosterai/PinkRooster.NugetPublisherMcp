using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using PinkRooster.NugetPublisher.Packaging;

namespace PinkRooster.NugetPublisher.Tests.Packaging;

public sealed class PackageArchiveInspectorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "pn_test_" + Guid.NewGuid().ToString("N"));

    public PackageArchiveInspectorTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Inspect_WhenValidNupkg_ExtractsMetadataAndDependencies()
    {
        // Arrange
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.TestPackage</id>
                <version>1.2.3</version>
                <authors>PinkRooster Team</authors>
                <description>A verified test package for archive inspector testing.</description>
                <license type="expression">MIT</license>
                <projectUrl>https://github.com/pinkroosterai/test</projectUrl>
                <tags>nuget publisher mcp test</tags>
                <dependencies>
                  <group targetFramework="net10.0">
                    <dependency id="AwesomeAssertions" version="8.0.1" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage(
            packageId: "PinkRooster.TestPackage",
            version: "1.2.3",
            nuspecXml: nuspec,
            additionalFiles: [("lib/net10.0/PinkRooster.TestPackage.dll", "mock-dll-content")]);

        // Act
        PackageArchiveInfo info = PackageArchiveInspector.Inspect(nupkgPath);

        // Assert
        info.PackageId.Should().Be("PinkRooster.TestPackage");
        info.Version.Should().Be("1.2.3");
        info.Authors.Should().Be("PinkRooster Team");
        info.Description.Should().Be("A verified test package for archive inspector testing.");
        info.LicenseExpression.Should().Be("MIT");
        info.LicenseFile.Should().BeNull();
        info.ProjectUrl.Should().Be("https://github.com/pinkroosterai/test");
        info.Tags.Should().Contain("nuget")
            .And.Contain("publisher")
            .And.Contain("mcp")
            .And.Contain("test");
        info.TargetFrameworks.Should().Contain("net10.0");
        info.Dependencies.Should().ContainSingle(d =>
            d.Id == "AwesomeAssertions" &&
            d.VersionRange == "8.0.1" &&
            d.TargetFramework == "net10.0");
        info.Files.Should().Contain("lib/net10.0/PinkRooster.TestPackage.dll");
        info.IsValidSemVer.Should().BeTrue();
        info.IsValidLicenseExpression.Should().BeTrue();
        info.IsValid.Should().BeTrue();
        info.ValidationErrors.Should().BeEmpty();
        info.HasCompanionSymbols.Should().BeFalse();
        info.CompanionSymbolsPath.Should().BeNull();
    }

    [Fact]
    public void Inspect_WhenLicenseExpressionInvalid_ReportsValidationFailure()
    {
        // Arrange (Review Focus #3: Invalid or malformed SPDX license expression)
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.InvalidLicensePkg</id>
                <version>2.0.0</version>
                <authors>PinkRooster Team</authors>
                <description>Package with invalid SPDX expression.</description>
                <license type="expression">NOT_A_VALID_SPDX_LICENSE</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage(
            packageId: "PinkRooster.InvalidLicensePkg",
            version: "2.0.0",
            nuspecXml: nuspec);

        // Act
        PackageArchiveInfo info = PackageArchiveInspector.Inspect(nupkgPath);

        // Assert
        info.IsValid.Should().BeFalse();
        info.IsValidLicenseExpression.Should().BeFalse();
        info.ValidationErrors.Should().NotBeEmpty();
        info.ValidationErrors.Should().Contain(e => e.Contains("NOT_A_VALID_SPDX_LICENSE") || e.Contains("SPDX"));
    }

    [Fact]
    public void Inspect_WhenCompanionSnupkgExists_DetectsSymbolPackage()
    {
        // Arrange
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.WithSymbols</id>
                <version>1.0.0</version>
                <authors>PinkRooster Team</authors>
                <description>Package with companion snupkg symbols.</description>
                <license type="expression">Apache-2.0</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage(
            packageId: "PinkRooster.WithSymbols",
            version: "1.0.0",
            nuspecXml: nuspec,
            createCompanionSnupkg: true);

        // Act
        PackageArchiveInfo info = PackageArchiveInspector.Inspect(nupkgPath);

        // Assert
        info.HasCompanionSymbols.Should().BeTrue();
        info.CompanionSymbolsPath.Should().NotBeNull();
        File.Exists(info.CompanionSymbolsPath!).Should().BeTrue();
        Path.GetFileName(info.CompanionSymbolsPath).Should().Be("PinkRooster.WithSymbols.1.0.0.snupkg");
    }

    [Fact]
    public void Inspect_WhenInvalidSemVer_ReportsValidationFailure()
    {
        // Arrange
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.InvalidVer</id>
                <version>not-a-semver</version>
                <authors>PinkRooster</authors>
                <description>Invalid semver package</description>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage(
            packageId: "PinkRooster.InvalidVer",
            version: "not-a-semver",
            nuspecXml: nuspec);

        // Act
        PackageArchiveInfo info = PackageArchiveInspector.Inspect(nupkgPath);

        // Assert
        info.IsValidSemVer.Should().BeFalse();
        info.IsValid.Should().BeFalse();
        info.ValidationErrors.Should().Contain(e => e.Contains("SemVer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Inspect_WhenLicenseFileSpecifiedAndPresent_ValidatesSuccessfully()
    {
        // Arrange
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.FileLicensePkg</id>
                <version>1.0.0</version>
                <authors>PinkRooster</authors>
                <description>Package with license file</description>
                <license type="file">LICENSE.txt</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage(
            packageId: "PinkRooster.FileLicensePkg",
            version: "1.0.0",
            nuspecXml: nuspec,
            additionalFiles: [("LICENSE.txt", "Custom license terms")]);

        // Act
        PackageArchiveInfo info = PackageArchiveInspector.Inspect(nupkgPath);

        // Assert
        info.LicenseFile.Should().Be("LICENSE.txt");
        info.LicenseExpression.Should().BeNull();
        info.IsValidLicenseExpression.Should().BeTrue();
        info.IsValid.Should().BeTrue();
        info.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_WhenLicenseFileSpecifiedAndMissing_ReportsValidationFailure()
    {
        // Arrange
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.MissingFileLicensePkg</id>
                <version>1.0.0</version>
                <authors>PinkRooster</authors>
                <description>Package missing license file</description>
                <license type="file">LICENSE.txt</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage(
            packageId: "PinkRooster.MissingFileLicensePkg",
            version: "1.0.0",
            nuspecXml: nuspec);

        // Act
        PackageArchiveInfo info = PackageArchiveInspector.Inspect(nupkgPath);

        // Assert
        info.IsValidLicenseExpression.Should().BeFalse();
        info.IsValid.Should().BeFalse();
        info.ValidationErrors.Should().Contain(e => e.Contains("LICENSE.txt"));
    }

    [Fact]
    public void Inspect_WhenFileNotFound_ThrowsFileNotFoundException()
    {
        string missingPath = Path.Combine(_tempDir, "NonExistent.1.0.0.nupkg");

        Action act = () => PackageArchiveInspector.Inspect(missingPath);

        act.Should().Throw<FileNotFoundException>();
    }

    private string CreatePackage(
        string packageId,
        string version,
        string nuspecXml,
        bool createCompanionSnupkg = false,
        IEnumerable<(string path, string content)>? additionalFiles = null)
    {
        string nupkgPath = Path.Combine(_tempDir, $"{packageId}.{version}.nupkg");

        using (FileStream fs = File.Create(nupkgPath))
        using (ZipArchive archive = new(fs, ZipArchiveMode.Create))
        {
            ZipArchiveEntry nuspecEntry = archive.CreateEntry($"{packageId}.nuspec");
            using (Stream entryStream = nuspecEntry.Open())
            using (StreamWriter writer = new(entryStream, Encoding.UTF8))
            {
                writer.Write(nuspecXml);
            }

            if (additionalFiles != null)
            {
                foreach ((string path, string content) in additionalFiles)
                {
                    ZipArchiveEntry fileEntry = archive.CreateEntry(path);
                    using Stream entryStream = fileEntry.Open();
                    using StreamWriter writer = new(entryStream, Encoding.UTF8);
                    writer.Write(content);
                }
            }
        }

        if (createCompanionSnupkg)
        {
            string snupkgPath = Path.Combine(_tempDir, $"{packageId}.{version}.snupkg");
            using FileStream fs = File.Create(snupkgPath);
            using ZipArchive archive = new(fs, ZipArchiveMode.Create);
            ZipArchiveEntry entry = archive.CreateEntry($"{packageId}.pdb");
            using Stream entryStream = entry.Open();
            using StreamWriter writer = new(entryStream, Encoding.UTF8);
            writer.Write("mock-pdb");
        }

        return nupkgPath;
    }
}
