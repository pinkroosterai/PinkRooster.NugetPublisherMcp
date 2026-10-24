using System.Net;
using AwesomeAssertions;
using PinkRooster.NugetPublisher.Administration;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Feeds.Models;

namespace PinkRooster.NugetPublisher.Tests.Administration;

public sealed class PackageAdministrationServiceTests
{
    [Fact]
    public async Task Unlist_WhenValid_SendsDeleteAndReturnsUnlistedStatus()
    {
        // Arrange
        var feedClient = new MockNugetFeedClient
        {
            UnlistPackageHandler = (id, ver, feed, key) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)),
        };
        var service = new PackageAdministrationService(feedClient);

        var request = new ManageVersionRequest
        {
            PackageId = "PinkRooster.NugetPublisher",
            Version = "1.0.0",
            Action = ManageVersionAction.Unlist,
            FeedUrl = "https://api.nuget.org/v3/index.json",
            ApiKey = "oy2a-valid-api-key-1234567890",
            DryRun = false,
        };

        // Act
        ManageVersionResponse response = await service.ManageVersionAsync(
            request,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.PackageId.Should().Be("PinkRooster.NugetPublisher");
        response.Version.Should().Be("1.0.0");
        response.Action.Should().Be("unlist");
        response.DryRun.Should().BeFalse();
        response.PreviousStatus.Should().Be("Listed");
        response.CurrentStatus.Should().Be("Unlisted");
        response.StatusCode.Should().Be(204);
        response.Message.Should().Contain("successfully unlisted");

        feedClient.UnlistCalls.Should().Be(1);
        feedClient.RelistCalls.Should().Be(0);
        feedClient.LastUnlistPackageId.Should().Be("PinkRooster.NugetPublisher");
        feedClient.LastUnlistVersion.Should().Be("1.0.0");
        feedClient.LastUnlistFeedUrl.Should().Be("https://api.nuget.org/v3/index.json");
        feedClient.LastUnlistApiKey.Should().Be("oy2a-valid-api-key-1234567890");
    }

    [Fact]
    public async Task Relist_WhenValid_SendsPostAndReturnsListedStatus()
    {
        // Arrange
        var feedClient = new MockNugetFeedClient
        {
            RelistPackageHandler = (id, ver, feed, key) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
        };
        var service = new PackageAdministrationService(feedClient);

        var request = new ManageVersionRequest
        {
            PackageId = "PinkRooster.NugetPublisher",
            Version = "1.0.0",
            Action = ManageVersionAction.Relist,
            FeedUrl = "https://api.nuget.org/v3/index.json",
            ApiKey = "oy2a-valid-api-key-1234567890",
            DryRun = false,
        };

        // Act
        ManageVersionResponse response = await service.ManageVersionAsync(
            request,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.PackageId.Should().Be("PinkRooster.NugetPublisher");
        response.Version.Should().Be("1.0.0");
        response.Action.Should().Be("relist");
        response.DryRun.Should().BeFalse();
        response.PreviousStatus.Should().Be("Unlisted");
        response.CurrentStatus.Should().Be("Listed");
        response.StatusCode.Should().Be(200);
        response.Message.Should().Contain("successfully relisted");

        feedClient.RelistCalls.Should().Be(1);
        feedClient.UnlistCalls.Should().Be(0);
        feedClient.LastRelistPackageId.Should().Be("PinkRooster.NugetPublisher");
        feedClient.LastRelistVersion.Should().Be("1.0.0");
        feedClient.LastRelistFeedUrl.Should().Be("https://api.nuget.org/v3/index.json");
        feedClient.LastRelistApiKey.Should().Be("oy2a-valid-api-key-1234567890");
    }

    [Fact]
    public async Task Unlist_WhenDryRunTrue_QueriesFeedWithoutMutating()
    {
        // Arrange
        var feedClient = new MockNugetFeedClient
        {
            GetPackageMetadataHandler = (id, feed) =>
                Task.FromResult(
                    new PackageMetadataResult
                    {
                        PackageId = id,
                        Exists = true,
                        Versions = ["1.0.0"],
                        VersionDetails =
                        [
                            new PackageVersionMetadata
                            {
                                Version = "1.0.0",
                                Listed = true,
                            },
                        ],
                    }
                ),
        };
        var service = new PackageAdministrationService(feedClient);

        var request = new ManageVersionRequest
        {
            PackageId = "PinkRooster.NugetPublisher",
            Version = "1.0.0",
            Action = ManageVersionAction.Unlist,
            DryRun = true,
        };

        // Act
        ManageVersionResponse response = await service.ManageVersionAsync(
            request,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.DryRun.Should().BeTrue();
        response.PackageId.Should().Be("PinkRooster.NugetPublisher");
        response.Version.Should().Be("1.0.0");
        response.Action.Should().Be("unlist");
        response.PreviousStatus.Should().Be("Listed");
        response.CurrentStatus.Should().Be("Unlisted");
        response.StatusCode.Should().Be(200);
        response.Message.Should().Contain("[DryRun]");

        feedClient.UnlistCalls.Should().Be(0);
        feedClient.RelistCalls.Should().Be(0);
        feedClient.GetMetadataCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Unlist_WhenFeedReturns405MethodNotAllowed_ReturnsActionableGuidance()
    {
        // Arrange
        var feedClient = new MockNugetFeedClient
        {
            UnlistPackageHandler = (id, ver, feed, key) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
                    {
                        Content = new StringContent("Method Not Allowed"),
                    }
                ),
        };
        var service = new PackageAdministrationService(feedClient);

        var request = new ManageVersionRequest
        {
            PackageId = "PinkRooster.NugetPublisher",
            Version = "1.0.0",
            Action = ManageVersionAction.Unlist,
            FeedUrl = "https://nuget.pkg.github.com/owner/index.json",
            DryRun = false,
        };

        // Act
        ManageVersionResponse response = await service.ManageVersionAsync(
            request,
            TestContext.Current.CancellationToken
        );

        // Assert (Review Focus #4: Actionable guidance on 405 Method Not Allowed)
        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(405);
        response.CurrentStatus.Should().BeNull();
        response.Message.Should().Contain("405");
        response.Message.Should().Contain("Method Not Allowed");
        response.Message.Should().Contain("DELETE");
        response.Message.Should().Contain("portal");
    }

    [Fact]
    public void GetDeprecationPortalUrl_GeneratesValidNugetOrgPortalLink()
    {
        // Arrange
        var service = new PackageAdministrationService(new MockNugetFeedClient());
        var request = new DeprecationUrlRequest
        {
            PackageId = "PinkRooster.NugetPublisher",
            Version = "1.0.0",
        };

        // Act
        string url = service.GetDeprecationPortalUrl(request);

        // Assert
        url.Should().Be("https://www.nuget.org/packages/PinkRooster.NugetPublisher/1.0.0/deprecate");
    }

    [Fact]
    public void GetDeprecationPortalUrl_WithoutVersion_GeneratesPackageLevelPortalLink()
    {
        // Arrange
        var service = new PackageAdministrationService(new MockNugetFeedClient());
        var request = new DeprecationUrlRequest
        {
            PackageId = "PinkRooster.NugetPublisher",
            Version = null,
        };

        // Act
        string url = service.GetDeprecationPortalUrl(request);

        // Assert
        url.Should().Be("https://www.nuget.org/packages/PinkRooster.NugetPublisher/deprecate");
    }

    [Fact]
    public void GetDeprecationPortalUrl_WithGuidanceParameters_AppendsQueryParameters()
    {
        // Arrange
        var service = new PackageAdministrationService(new MockNugetFeedClient());
        var request = new DeprecationUrlRequest
        {
            PackageId = "PinkRooster.NugetPublisher",
            Version = "1.0.0",
            Reason = "Legacy",
            AlternatePackageId = "PinkRooster.Modern",
            Message = "Use modern package instead",
        };

        // Act
        string url = service.GetDeprecationPortalUrl(request);

        // Assert
        url.Should().StartWith("https://www.nuget.org/packages/PinkRooster.NugetPublisher/1.0.0/deprecate?");
        url.Should().Contain("reason=Legacy");
        url.Should().Contain("alternatePackageId=PinkRooster.Modern");
        url.Should().Contain("message=Use%20modern%20package%20instead");
    }

    [Fact]
    public async Task Unlist_WhenNotFound_Returns404Failure()
    {
        // Arrange
        var feedClient = new MockNugetFeedClient
        {
            UnlistPackageHandler = (id, ver, feed, key) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)),
        };
        var service = new PackageAdministrationService(feedClient);

        // Act
        ManageVersionResponse response = await service.ManageVersionAsync(
            new ManageVersionRequest
            {
                PackageId = "PinkRooster.Missing",
                Version = "9.9.9",
                Action = ManageVersionAction.Unlist,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(404);
        response.CurrentStatus.Should().BeNull();
        response.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Unlist_WhenForbidden_Returns403Failure()
    {
        // Arrange
        var feedClient = new MockNugetFeedClient
        {
            UnlistPackageHandler = (id, ver, feed, key) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)),
        };
        var service = new PackageAdministrationService(feedClient);

        // Act
        ManageVersionResponse response = await service.ManageVersionAsync(
            new ManageVersionRequest
            {
                PackageId = "PinkRooster.Forbidden",
                Version = "1.0.0",
                Action = ManageVersionAction.Unlist,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(403);
        response.CurrentStatus.Should().BeNull();
        response.Message.Should().Contain("forbidden");
    }

    [Fact]
    public async Task Relist_WhenDryRunTrue_QueriesFeedWithoutMutating()
    {
        // Arrange
        var feedClient = new MockNugetFeedClient
        {
            GetPackageMetadataHandler = (id, feed) =>
                Task.FromResult(
                    new PackageMetadataResult
                    {
                        PackageId = id,
                        Exists = true,
                        Versions = ["1.0.0"],
                        VersionDetails =
                        [
                            new PackageVersionMetadata
                            {
                                Version = "1.0.0",
                                Listed = false,
                            },
                        ],
                    }
                ),
        };
        var service = new PackageAdministrationService(feedClient);

        var request = new ManageVersionRequest
        {
            PackageId = "PinkRooster.NugetPublisher",
            Version = "1.0.0",
            Action = ManageVersionAction.Relist,
            DryRun = true,
        };

        // Act
        ManageVersionResponse response = await service.ManageVersionAsync(
            request,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeTrue();
        response.DryRun.Should().BeTrue();
        response.PreviousStatus.Should().Be("Unlisted");
        response.CurrentStatus.Should().Be("Listed");
        response.StatusCode.Should().Be(200);
        response.Message.Should().Contain("[DryRun]");

        feedClient.RelistCalls.Should().Be(0);
        feedClient.UnlistCalls.Should().Be(0);
    }

    [Fact]
    public async Task Unlist_WhenDryRunTrueAndVersionNotFound_Returns404()
    {
        // Arrange
        var feedClient = new MockNugetFeedClient
        {
            GetPackageMetadataHandler = (id, feed) =>
                Task.FromResult(new PackageMetadataResult { PackageId = id, Exists = false }),
            DoesVersionExistHandler = (id, ver, feed) => Task.FromResult(false),
        };
        var service = new PackageAdministrationService(feedClient);

        // Act
        ManageVersionResponse response = await service.ManageVersionAsync(
            new ManageVersionRequest
            {
                PackageId = "PinkRooster.NugetPublisher",
                Version = "1.0.0",
                Action = ManageVersionAction.Unlist,
                DryRun = true,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(404);
        response.Message.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ManageVersion_SanitizesSensitiveApiKeyInMessages()
    {
        // Arrange
        const string rawSecret = "oy2a-super-secret-key-999888777666";
        var feedClient = new MockNugetFeedClient
        {
            UnlistPackageHandler = (id, ver, feed, key) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent($"Error authenticating with key {rawSecret}."),
                    }
                ),
        };
        var service = new PackageAdministrationService(feedClient);

        // Act
        ManageVersionResponse response = await service.ManageVersionAsync(
            new ManageVersionRequest
            {
                PackageId = "PinkRooster.NugetPublisher",
                Version = "1.0.0",
                Action = ManageVersionAction.Unlist,
                ApiKey = rawSecret,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        response.Success.Should().BeFalse();
        response.Message.Should().NotContain(rawSecret);
        response.Message.Should().Contain("***REDACTED***");
    }

    [Fact]
    public async Task ManageVersion_ThrowsWhenRequestIsNull_OrPackageIdMissing()
    {
        var service = new PackageAdministrationService(new MockNugetFeedClient());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ManageVersionAsync(null!, TestContext.Current.CancellationToken)
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ManageVersionAsync(
                new ManageVersionRequest { PackageId = "", Version = "1.0.0" },
                TestContext.Current.CancellationToken
            )
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ManageVersionAsync(
                new ManageVersionRequest { PackageId = "Test", Version = "" },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public void GetDeprecationPortalUrl_ThrowsWhenRequestIsNull_OrPackageIdMissing()
    {
        var service = new PackageAdministrationService(new MockNugetFeedClient());

        Assert.Throws<ArgumentNullException>(() => service.GetDeprecationPortalUrl(null!));
        Assert.Throws<ArgumentException>(() =>
            service.GetDeprecationPortalUrl(new DeprecationUrlRequest { PackageId = "" })
        );
    }

    private sealed class MockNugetFeedClient : INugetFeedClient
    {
        public int UnlistCalls { get; private set; }
        public int RelistCalls { get; private set; }
        public int GetMetadataCalls { get; private set; }
        public int DoesVersionExistCalls { get; private set; }

        public string? LastUnlistPackageId { get; private set; }
        public string? LastUnlistVersion { get; private set; }
        public string? LastUnlistFeedUrl { get; private set; }
        public string? LastUnlistApiKey { get; private set; }

        public string? LastRelistPackageId { get; private set; }
        public string? LastRelistVersion { get; private set; }
        public string? LastRelistFeedUrl { get; private set; }
        public string? LastRelistApiKey { get; private set; }

        public Func<string, string?, Task<PackageMetadataResult>>? GetPackageMetadataHandler { get; init; }
        public Func<string, string, string?, Task<bool>>? DoesVersionExistHandler { get; init; }
        public Func<string, string, string?, string?, Task<HttpResponseMessage>>? UnlistPackageHandler { get; init; }
        public Func<string, string, string?, string?, Task<HttpResponseMessage>>? RelistPackageHandler { get; init; }

        public Task<HttpResponseMessage> UnlistPackageAsync(
            string packageId,
            string version,
            string? feedUrl = null,
            string? apiKey = null,
            CancellationToken ct = default
        )
        {
            UnlistCalls++;
            LastUnlistPackageId = packageId;
            LastUnlistVersion = version;
            LastUnlistFeedUrl = feedUrl;
            LastUnlistApiKey = apiKey;
            if (UnlistPackageHandler is not null)
            {
                return UnlistPackageHandler(packageId, version, feedUrl, apiKey);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }

        public Task<HttpResponseMessage> RelistPackageAsync(
            string packageId,
            string version,
            string? feedUrl = null,
            string? apiKey = null,
            CancellationToken ct = default
        )
        {
            RelistCalls++;
            LastRelistPackageId = packageId;
            LastRelistVersion = version;
            LastRelistFeedUrl = feedUrl;
            LastRelistApiKey = apiKey;
            if (RelistPackageHandler is not null)
            {
                return RelistPackageHandler(packageId, version, feedUrl, apiKey);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        public Task<PackageMetadataResult> GetPackageMetadataAsync(
            string packageId,
            string? feedUrl = null,
            CancellationToken ct = default
        )
        {
            GetMetadataCalls++;
            if (GetPackageMetadataHandler is not null)
            {
                return GetPackageMetadataHandler(packageId, feedUrl);
            }
            return Task.FromResult(new PackageMetadataResult { PackageId = packageId, Exists = false });
        }

        public Task<bool> DoesVersionExistAsync(
            string packageId,
            string version,
            string? feedUrl = null,
            CancellationToken ct = default
        )
        {
            DoesVersionExistCalls++;
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

        public Task<IReadOnlyList<PackageSearchResult>> SearchPackagesAsync(
            string query,
            int take = 20,
            bool prerelease = false,
            string? feedUrl = null,
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
    }
}
