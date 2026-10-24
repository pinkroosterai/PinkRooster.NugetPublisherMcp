using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Feeds.Models;
using PinkRooster.NugetPublisher.Packaging;
using PinkRooster.NugetPublisher.Validation;

namespace PinkRooster.NugetPublisher.Tests.Validation;

public sealed class PublishSafetyValidatorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "pn_val_test_" + Guid.NewGuid().ToString("N")
    );

    public PublishSafetyValidatorTests()
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
    public async Task Validate_WhenPackageValidAndVersionDoesNotExist_Passes()
    {
        // Arrange
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.SafePackage</id>
                <version>1.0.0</version>
                <authors>PinkRooster Team</authors>
                <description>A valid package safe for publish</description>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage("PinkRooster.SafePackage", "1.0.0", nuspec);

        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
        };

        var validator = new PublishSafetyValidator(feedClient);

        // Act
        ValidationResult result = await validator.ValidateAsync(
            nupkgPath,
            "https://api.nuget.org/v3/index.json",
            TestContext.Current.CancellationToken
        );

        // Assert
        result.IsValid.Should().BeTrue();
        result.HasCollision.Should().BeFalse();
        result.PackageId.Should().Be("PinkRooster.SafePackage");
        result.Version.Should().Be("1.0.0");
        result.ArchiveInfo.Should().NotBeNull();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenPackageVersionAlreadyExistsOnFeed_FailsValidation()
    {
        // Arrange (Review Focus #1: Remote feed collision check)
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.CollisionPackage</id>
                <version>2.5.0</version>
                <authors>PinkRooster Team</authors>
                <description>A package that already exists remotely</description>
                <license type="expression">Apache-2.0</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage("PinkRooster.CollisionPackage", "2.5.0", nuspec);

        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(true),
        };

        var validator = new PublishSafetyValidator(feedClient);

        // Act
        ValidationResult result = await validator.ValidateAsync(
            nupkgPath,
            "https://api.nuget.org/v3/index.json",
            TestContext.Current.CancellationToken
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasCollision.Should().BeTrue();
        result.PackageId.Should().Be("PinkRooster.CollisionPackage");
        result.Version.Should().Be("2.5.0");
        result
            .Issues.Should()
            .ContainSingle(i =>
                i.Severity == ValidationSeverity.Error
                && i.Code == ValidationIssueCodes.VersionAlreadyExists
                && i.Message.Contains("2.5.0")
                && i.Message.Contains("PinkRooster.CollisionPackage")
                && i.Message.Contains("immutable")
            );
    }

    [Fact]
    public async Task Validate_WhenNuspecMalformed_FailsValidation()
    {
        // Arrange
        const string brokenNuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.Broken</id>
                <version>1.0.0</version>
                <!-- Missing closing tags
            """;

        string nupkgPath = CreatePackage("PinkRooster.Broken", "1.0.0", brokenNuspec);

        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
        };

        var validator = new PublishSafetyValidator(feedClient);

        // Act
        ValidationResult result = await validator.ValidateAsync(
            nupkgPath,
            "https://api.nuget.org/v3/index.json",
            TestContext.Current.CancellationToken
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.ArchiveInfo.Should().BeNull();
        result.Issues.Should().Contain(i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task Validate_WhenCompanionSymbolsMissingAndExpected_RecordsWarning()
    {
        // Arrange (Review Focus #2: Missing companion .snupkg when symbol push is enabled)
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.MissingSymbols</id>
                <version>1.0.0</version>
                <authors>PinkRooster Team</authors>
                <description>Package without symbols</description>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage(
            "PinkRooster.MissingSymbols",
            "1.0.0",
            nuspec,
            createCompanionSnupkg: false
        );

        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
        };

        var validator = new PublishSafetyValidator(feedClient);

        // Act
        ValidationResult result = await validator.ValidateAsync(
            nupkgPath,
            feedUrl: null,
            expectSymbols: true,
            TestContext.Current.CancellationToken
        );

        // Assert: Warning only; IsValid must remain true
        result.IsValid.Should().BeTrue();
        result.HasCollision.Should().BeFalse();
        result
            .Issues.Should()
            .ContainSingle(i =>
                i.Severity == ValidationSeverity.Warning
                && i.Code == ValidationIssueCodes.SymbolPackageNotFound
            );
    }

    [Fact]
    public async Task Validate_WhenCompanionSymbolsPresentAndExpected_HasNoSymbolWarning()
    {
        // Arrange
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.WithSymbols</id>
                <version>1.0.0</version>
                <authors>PinkRooster Team</authors>
                <description>Package with symbols</description>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage(
            "PinkRooster.WithSymbols",
            "1.0.0",
            nuspec,
            createCompanionSnupkg: true
        );

        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
        };

        var validator = new PublishSafetyValidator(feedClient);

        // Act
        ValidationResult result = await validator.ValidateAsync(
            nupkgPath,
            feedUrl: null,
            expectSymbols: true,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.IsValid.Should().BeTrue();
        result
            .Issues.Should()
            .NotContain(i => i.Code == ValidationIssueCodes.SymbolPackageNotFound);
    }

    [Fact]
    public async Task Validate_WhenSemVerInvalid_FailsValidation()
    {
        // Arrange
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.BadSemVer</id>
                <version>not-a-valid-semver</version>
                <authors>PinkRooster Team</authors>
                <description>Invalid semver package</description>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage("PinkRooster.BadSemVer", "not-a-valid-semver", nuspec);

        var feedClient = new MockNugetFeedClient();
        var validator = new PublishSafetyValidator(feedClient);

        // Act
        ValidationResult result = await validator.ValidateAsync(
            nupkgPath,
            ct: TestContext.Current.CancellationToken
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result
            .Issues.Should()
            .Contain(i =>
                i.Severity == ValidationSeverity.Error
                && i.Code == ValidationIssueCodes.InvalidSemVer
            );
    }

    [Fact]
    public async Task Validate_WhenLicenseInvalid_FailsValidation()
    {
        // Arrange
        const string nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.BadLicense</id>
                <version>1.0.0</version>
                <authors>PinkRooster Team</authors>
                <description>Invalid license package</description>
                <license type="expression">NON_EXISTENT_SPDX_123</license>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage("PinkRooster.BadLicense", "1.0.0", nuspec);

        var feedClient = new MockNugetFeedClient();
        var validator = new PublishSafetyValidator(feedClient);

        // Act
        ValidationResult result = await validator.ValidateAsync(
            nupkgPath,
            ct: TestContext.Current.CancellationToken
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result
            .Issues.Should()
            .Contain(i =>
                i.Severity == ValidationSeverity.Error
                && i.Code == ValidationIssueCodes.InvalidLicense
            );
    }

    [Fact]
    public async Task Validate_WhenPackageFileNotFound_FailsValidation()
    {
        // Arrange
        string nonExistentPath = Path.Combine(_tempDir, "DoesNotExist.1.0.0.nupkg");
        var feedClient = new MockNugetFeedClient();
        var validator = new PublishSafetyValidator(feedClient);

        // Act
        ValidationResult result = await validator.ValidateAsync(
            nonExistentPath,
            ct: TestContext.Current.CancellationToken
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.ArchiveInfo.Should().BeNull();
        result
            .Issues.Should()
            .Contain(i =>
                i.Severity == ValidationSeverity.Error
                && i.Code == ValidationIssueCodes.PackageFileNotFound
            );
    }

    private string CreatePackage(
        string packageId,
        string version,
        string nuspecXml,
        bool createCompanionSnupkg = false
    )
    {
        string nupkgPath = Path.Combine(_tempDir, $"{packageId}.{version}.nupkg");

        using (FileStream fs = File.Create(nupkgPath))
        using (ZipArchive archive = new(fs, ZipArchiveMode.Create))
        {
            ZipArchiveEntry nuspecEntry = archive.CreateEntry($"{packageId}.nuspec");
            using Stream entryStream = nuspecEntry.Open();
            using StreamWriter writer = new(entryStream, Encoding.UTF8);
            writer.Write(nuspecXml);
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

    private sealed class MockNugetFeedClient : INugetFeedClient
    {
        public Func<string, string, string?, Task<bool>>? DoesVersionExistHandler { get; init; }

        public Task<bool> DoesVersionExistAsync(
            string packageId,
            string version,
            string? feedUrl = null,
            CancellationToken ct = default
        )
        {
            if (DoesVersionExistHandler is not null)
            {
                return DoesVersionExistHandler(packageId, version, feedUrl);
            }

            return Task.FromResult(false);
        }

        public Task<FeedServiceEndpoints> GetServiceEndpointsAsync(
            string? feedUrl = null,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<PackageMetadataResult> GetPackageMetadataAsync(
            string packageId,
            string? feedUrl = null,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyList<PackageSearchResult>> SearchPackagesAsync(
            string query,
            int take = 20,
            bool prerelease = false,
            string? feedUrl = null,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<PublishedPackagesResult> ListPublishedPackagesAsync(
            PublishedPackagesQuery query,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<PushResult> PushPackageAsync(
            Stream stream,
            string? feedUrl = null,
            string? apiKey = null,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<PushResult> PushSymbolPackageAsync(
            Stream stream,
            string? symbolFeedUrl = null,
            string? apiKey = null,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<HttpResponseMessage> UnlistPackageAsync(
            string packageId,
            string version,
            string? feedUrl = null,
            string? apiKey = null,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<HttpResponseMessage> RelistPackageAsync(
            string packageId,
            string version,
            string? feedUrl = null,
            string? apiKey = null,
            CancellationToken ct = default
        ) => throw new NotImplementedException();
    }
}
