using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Feeds.Models;
using PinkRooster.NugetPublisher.Publishing;

namespace PinkRooster.NugetPublisher.Tests.Publishing;

public sealed class PackagePublisherServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "pn_pub_test_" + Guid.NewGuid().ToString("N")
    );

    public PackagePublisherServiceTests()
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
    public async Task Publish_WhenPreflightFails_AbortsAndReturnsDiagnosticsWithoutPush()
    {
        // Arrange
        const string malformedNuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <!-- Missing id, version, etc -->
                <authors>PinkRooster Team</authors>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage("PinkRooster.Invalid", "1.0.0", malformedNuspec);
        var feedClient = new MockNugetFeedClient();
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = "oy2a-valid-api-key-1234567890",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeFalse();
        response.PublishedPackage.Should().BeFalse();
        response.PublishedSymbols.Should().BeFalse();
        response.StatusCode.Should().Be(400);
        response.Message.Should().Contain("pre-flight validation failed");
        feedClient.PushPackageCalls.Should().Be(0);
        feedClient.PushSymbolCalls.Should().Be(0);
    }

    [Fact]
    public async Task Publish_WhenPreflightDetectsCollisionAndSkipDuplicateFalse_AbortsWith409()
    {
        // Arrange
        string nupkgPath = CreateValidPackage("PinkRooster.Colliding", "1.2.3");
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(true),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = "oy2a-valid-api-key-1234567890",
                SkipDuplicate = false,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeFalse();
        response.PublishedPackage.Should().BeFalse();
        response.StatusCode.Should().Be(409);
        response.Message.Should().Contain("already exists on feed");
        feedClient.PushPackageCalls.Should().Be(0);
        feedClient.PushSymbolCalls.Should().Be(0);
    }

    [Fact]
    public async Task Publish_WhenPreflightDetectsCollisionAndSkipDuplicateTrue_ReturnsSuccessWithWarningWithoutPush()
    {
        // Arrange
        string nupkgPath = CreateValidPackage("PinkRooster.SkippedCollision", "1.2.3");
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(true),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = "oy2a-valid-api-key-1234567890",
                SkipDuplicate = true,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.PublishedPackage.Should().BeFalse();
        response.PublishedSymbols.Should().BeFalse();
        response.StatusCode.Should().Be(409);
        response
            .Warnings.Should()
            .Contain(w => w.Contains("already exists on feed") && w.Contains("skipped"));
        feedClient.PushPackageCalls.Should().Be(0);
        feedClient.PushSymbolCalls.Should().Be(0);
    }

    [Fact]
    public async Task Publish_WhenPreflightFailsWithForce_PushesPackage()
    {
        // Arrange
        const string nuspecWithoutLicense = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>PinkRooster.ForcedPackage</id>
                <version>1.0.0</version>
                <authors>PinkRooster Team</authors>
                <description>Missing license expression</description>
              </metadata>
            </package>
            """;

        string nupkgPath = CreatePackage(
            "PinkRooster.ForcedPackage",
            "1.0.0",
            nuspecWithoutLicense
        );
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
            PushPackageHandler = (stream, feed, key) =>
                Task.FromResult(
                    new PushResult
                    {
                        Success = true,
                        StatusCode = 201,
                        Message = "Package pushed successfully.",
                    }
                ),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = "oy2a-valid-api-key-1234567890",
                Force = true,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.PublishedPackage.Should().BeTrue();
        feedClient.PushPackageCalls.Should().Be(1);
        response.Warnings.Should().Contain(w => w.Contains("Forced publish"));
    }

    [Fact]
    public async Task Publish_WhenValid_PushesNupkgThenSnupkgSequentially()
    {
        // Arrange
        string nupkgPath = CreateValidPackage(
            "PinkRooster.FullPublish",
            "1.0.0",
            createCompanionSnupkg: true
        );
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
            PushPackageHandler = (stream, feed, key) =>
                Task.FromResult(
                    new PushResult
                    {
                        Success = true,
                        StatusCode = 201,
                        Message = "Package pushed successfully.",
                    }
                ),
            PushSymbolHandler = (stream, feed, key) =>
                Task.FromResult(
                    new PushResult
                    {
                        Success = true,
                        StatusCode = 201,
                        Message = "Symbols pushed successfully.",
                    }
                ),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = "oy2a-valid-api-key-1234567890",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.PublishedPackage.Should().BeTrue();
        response.PublishedSymbols.Should().BeTrue();
        response.SymbolPackagePath.Should().NotBeNull();
        response.StatusCode.Should().Be(201);
        feedClient.CallSequence.Should().Equal(["nupkg", "snupkg"]);
    }

    [Fact]
    public async Task Publish_WhenCompanionSnupkgMissing_PushesNupkgAndReturnsWarning()
    {
        // Arrange (Review Focus #2)
        string nupkgPath = CreateValidPackage(
            "PinkRooster.NoSymbols",
            "1.0.0",
            createCompanionSnupkg: false
        );
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
            PushPackageHandler = (stream, feed, key) =>
                Task.FromResult(
                    new PushResult
                    {
                        Success = true,
                        StatusCode = 201,
                        Message = "Package pushed successfully.",
                    }
                ),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = "oy2a-valid-api-key-1234567890",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.PublishedPackage.Should().BeTrue();
        response.PublishedSymbols.Should().BeFalse();
        feedClient.PushPackageCalls.Should().Be(1);
        feedClient.PushSymbolCalls.Should().Be(0);
        response
            .Warnings.Should()
            .Contain(w => w.Contains("No companion symbol package (.snupkg) found"));
    }

    [Fact]
    public async Task Publish_WhenSymbolPushFails_PrimaryRemainsSuccessWithWarning()
    {
        // Arrange
        string nupkgPath = CreateValidPackage(
            "PinkRooster.SymbolFail",
            "1.0.0",
            createCompanionSnupkg: true
        );
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
            PushPackageHandler = (stream, feed, key) =>
                Task.FromResult(
                    new PushResult
                    {
                        Success = true,
                        StatusCode = 201,
                        Message = "Package pushed successfully.",
                    }
                ),
            PushSymbolHandler = (stream, feed, key) =>
                Task.FromResult(
                    new PushResult
                    {
                        Success = false,
                        StatusCode = 500,
                        Message = "Symbol indexing service timed out.",
                    }
                ),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = "oy2a-valid-api-key-1234567890",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.PublishedPackage.Should().BeTrue();
        response.PublishedSymbols.Should().BeFalse();
        feedClient.PushPackageCalls.Should().Be(1);
        feedClient.PushSymbolCalls.Should().Be(1);
        response.Warnings.Should().Contain(w => w.Contains("Companion symbol package push failed"));
    }

    [Fact]
    public async Task Publish_WhenPrimaryPushFails_AbortsSymbolsAndReturnsFailure()
    {
        // Arrange
        string nupkgPath = CreateValidPackage(
            "PinkRooster.PrimaryFail",
            "1.0.0",
            createCompanionSnupkg: true
        );
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
            PushPackageHandler = (stream, feed, key) =>
                Task.FromResult(
                    new PushResult
                    {
                        Success = false,
                        StatusCode = 403,
                        Message = "Forbidden: Insufficient privileges to publish package.",
                    }
                ),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = "oy2a-valid-api-key-1234567890",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeFalse();
        response.PublishedPackage.Should().BeFalse();
        response.PublishedSymbols.Should().BeFalse();
        response.StatusCode.Should().Be(403);
        response.Message.Should().Contain("Forbidden");
        feedClient.PushPackageCalls.Should().Be(1);
        feedClient.PushSymbolCalls.Should().Be(0);
    }

    [Fact]
    public async Task Publish_WhenConflictAndSkipDuplicateTrue_ReturnsSuccessWithWarning()
    {
        // Arrange
        string nupkgPath = CreateValidPackage("PinkRooster.RemoteConflict", "1.0.0");
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
            PushPackageHandler = (stream, feed, key) =>
                Task.FromResult(
                    new PushResult
                    {
                        Success = false,
                        StatusCode = 409,
                        Message = "A package with this ID and version already exists.",
                    }
                ),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = "oy2a-valid-api-key-1234567890",
                SkipDuplicate = true,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.PublishedPackage.Should().BeFalse();
        response.PublishedSymbols.Should().BeFalse();
        response.StatusCode.Should().Be(409);
        response
            .Warnings.Should()
            .Contain(w => w.Contains("already exists on feed") && w.Contains("skipped"));
        feedClient.PushSymbolCalls.Should().Be(0);
    }

    [Fact]
    public async Task Publish_WhenNoCredentialsResolved_ReturnsUnauthorizedWithoutPush()
    {
        // Arrange
        string nupkgPath = CreateValidPackage("PinkRooster.NoCreds", "1.0.0");
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://unauthenticated-feed.example.com/v3/index.json",
                ApiKey = null,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeFalse();
        response.PublishedPackage.Should().BeFalse();
        response.StatusCode.Should().Be(401);
        response.Message.Should().Contain("No API key");
        feedClient.PushPackageCalls.Should().Be(0);
    }

    [Fact]
    public async Task Publish_SanitizesSensitiveApiKeyInMessagesAndWarnings()
    {
        // Arrange
        const string secretKey = "oy2a-super-secret-token-abcdef123456";
        string nupkgPath = CreateValidPackage("PinkRooster.SanitizedSecret", "1.0.0");
        var feedClient = new MockNugetFeedClient
        {
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
            PushPackageHandler = (stream, feed, key) =>
                Task.FromResult(
                    new PushResult
                    {
                        Success = false,
                        StatusCode = 400,
                        Message = $"Server rejected key: {secretKey}",
                    }
                ),
        };
        var publisher = new PackagePublisherService(feedClient);

        // Act
        PublishPackageResponse response = await publisher.PublishAsync(
            new PublishPackageRequest
            {
                NupkgPath = nupkgPath,
                FeedUrl = "https://custom.feed/v3/index.json",
                ApiKey = secretKey,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Message.Should().NotContain(secretKey);
        response.Message.Should().Contain("***REDACTED***");
    }

    [Fact]
    public async Task Publish_ThrowsWhenRequestIsNull()
    {
        var publisher = new PackagePublisherService(new MockNugetFeedClient());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            publisher.PublishAsync(null!, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Publish_ThrowsWhenNupkgPathIsNullOrWhitespace()
    {
        var publisher = new PackagePublisherService(new MockNugetFeedClient());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.PublishAsync(
                new PublishPackageRequest { NupkgPath = "" },
                TestContext.Current.CancellationToken
            )
        );
    }

    private string CreateValidPackage(
        string packageId,
        string version,
        bool createCompanionSnupkg = false
    )
    {
        string nuspec = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{packageId}</id>
                <version>{version}</version>
                <authors>PinkRooster Team</authors>
                <description>A valid package</description>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        return CreatePackage(packageId, version, nuspec, createCompanionSnupkg);
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
        public List<string> CallSequence { get; } = [];
        public int PushPackageCalls { get; private set; }
        public int PushSymbolCalls { get; private set; }

        public Func<string, string, string?, Task<bool>>? DoesVersionExistHandler { get; init; }
        public Func<Stream, string?, string?, Task<PushResult>>? PushPackageHandler { get; init; }
        public Func<Stream, string?, string?, Task<PushResult>>? PushSymbolHandler { get; init; }

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

        public Task<PushResult> PushPackageAsync(
            Stream stream,
            string? feedUrl = null,
            string? apiKey = null,
            CancellationToken ct = default
        )
        {
            PushPackageCalls++;
            CallSequence.Add("nupkg");
            if (PushPackageHandler is not null)
            {
                return PushPackageHandler(stream, feedUrl, apiKey);
            }

            return Task.FromResult(
                new PushResult
                {
                    Success = true,
                    StatusCode = 200,
                    Message = "Package pushed.",
                }
            );
        }

        public Task<PushResult> PushSymbolPackageAsync(
            Stream stream,
            string? symbolFeedUrl = null,
            string? apiKey = null,
            CancellationToken ct = default
        )
        {
            PushSymbolCalls++;
            CallSequence.Add("snupkg");
            if (PushSymbolHandler is not null)
            {
                return PushSymbolHandler(stream, symbolFeedUrl, apiKey);
            }

            return Task.FromResult(
                new PushResult
                {
                    Success = true,
                    StatusCode = 200,
                    Message = "Symbols pushed.",
                }
            );
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
