using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using PinkRooster.NugetPublisher.Administration;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Feeds.Models;
using PinkRooster.NugetPublisher.Publishing;
using PinkRooster.NugetPublisher.Validation;
using PinkRooster.NugetPublisher.Vulnerabilities;
using PinkRooster.NugetPublisherMcp.Tools;

namespace PinkRooster.NugetPublisherMcp.Tests.Tools;

public sealed class NugetMcpToolsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "pn_mcp_test_" + Guid.NewGuid().ToString("N")
    );

    public NugetMcpToolsTests()
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
    public async Task NugetSearch_WhenCalled_DelegatesToFeedClientAndReturnsSerializedResult()
    {
        var feedClient = new MockFeedClient
        {
            SearchPackagesHandler = (query, take, prerelease, feedUrl) =>
            {
                var result = new PackageSearchResult
                {
                    Id = "PinkRooster.Sample",
                    Version = "1.2.3",
                    Description = "A sample package",
                    TotalDownloads = 42,
                    Authors = ["PinkRooster"],
                };
                return Task.FromResult<IReadOnlyList<PackageSearchResult>>([result]);
            },
        };
        var auditService = new MockVulnerabilityAuditService();
        var tools = new NugetDiscoveryTools(feedClient, auditService);

        CallToolResult result = await tools.nuget_search(
            "Sample",
            take: 10,
            prerelease: false,
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("PinkRooster.Sample");
        textBlock.Text.Should().Contain("1.2.3");
    }

    [Fact]
    public async Task NugetSearch_WhenOwnerAndAuthorProvided_CombinesThemIntoSearchQuery()
    {
        string? capturedQuery = null;
        var feedClient = new MockFeedClient
        {
            SearchPackagesHandler = (query, _, _, _) =>
            {
                capturedQuery = query;
                return Task.FromResult<IReadOnlyList<PackageSearchResult>>([]);
            },
        };
        var auditService = new MockVulnerabilityAuditService();
        var tools = new NugetDiscoveryTools(feedClient, auditService);

        CallToolResult result = await tools.nuget_search(
            query: "Prompt",
            owner: "PinkRoosterAi",
            author: "PinkRooster",
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        capturedQuery.Should().Be("owner:PinkRoosterAi author:PinkRooster Prompt");
    }

    [Fact]
    public async Task ListPublishedPackages_WhenCalledWithOwner_ReturnsStructuredJsonResult()
    {
        PublishedPackagesQuery? capturedQuery = null;
        var feedClient = new MockFeedClient
        {
            ListPublishedPackagesHandler = query =>
            {
                capturedQuery = query;
                return Task.FromResult(
                    new PublishedPackagesResult
                    {
                        TotalHits = 1,
                        Owner = query.Owner,
                        FeedUrl = "https://api.nuget.org/v3/index.json",
                        Packages =
                        [
                            new PackageSearchResult
                            {
                                Id = "PinkRooster.PromptComposer",
                                Version = "1.1.1",
                                Description = "AI prompt library",
                                Authors = ["PinkRooster"],
                                Owners = ["PinkRoosterAi"],
                                TotalDownloads = 279,
                            },
                        ],
                    }
                );
            },
        };
        var auditService = new MockVulnerabilityAuditService();
        var tools = new NugetDiscoveryTools(feedClient, auditService);

        CallToolResult result = await tools.nuget_list_published(
            owner: "PinkRoosterAi",
            take: 10,
            skip: 0,
            prerelease: true,
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("PinkRooster.PromptComposer");
        textBlock.Text.Should().Contain("1.1.1");
        textBlock.Text.Should().Contain("PinkRoosterAi");
        textBlock.Text.Should().Contain("279");
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Owner.Should().Be("PinkRoosterAi");
        capturedQuery.Take.Should().Be(10);
        capturedQuery.Prerelease.Should().BeTrue();
    }

    [Fact]
    public async Task ListPublishedPackages_WhenCalledWithAuthorAndQuery_DelegatesToFeedClient()
    {
        PublishedPackagesQuery? capturedQuery = null;
        var feedClient = new MockFeedClient
        {
            ListPublishedPackagesHandler = query =>
            {
                capturedQuery = query;
                return Task.FromResult(new PublishedPackagesResult { TotalHits = 0 });
            },
        };
        var auditService = new MockVulnerabilityAuditService();
        var tools = new NugetDiscoveryTools(feedClient, auditService);

        CallToolResult result = await tools.ListPublishedPackages(
            author: "PinkRooster",
            query: "Composer",
            prerelease: false,
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Author.Should().Be("PinkRooster");
        capturedQuery.Query.Should().Be("Composer");
        capturedQuery.Prerelease.Should().BeFalse();
    }

    [Fact]
    public async Task ListPublishedPackages_WhenExceptionThrownWithSecret_MasksSecretsInErrorResponse()
    {
        const string rawSecret = "ghp_mockListSecretPatToken123456789012345678";
        var feedClient = new MockFeedClient
        {
            ListPublishedPackagesHandler = _ =>
                throw new InvalidOperationException($"Feed query failed: Bearer {rawSecret}"),
        };
        var auditService = new MockVulnerabilityAuditService();
        var tools = new NugetDiscoveryTools(feedClient, auditService);

        CallToolResult result = await tools.nuget_list_published(
            owner: "PinkRoosterAi",
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().NotContain(rawSecret);
        textBlock.Text.Should().Contain("***REDACTED***");
    }

    [Fact]
    public async Task NugetGetMetadata_WhenCalled_ReturnsPackageMetadata()
    {
        var feedClient = new MockFeedClient
        {
            GetPackageMetadataHandler = (packageId, feedUrl) =>
                Task.FromResult(
                    new PackageMetadataResult
                    {
                        PackageId = packageId,
                        Versions = ["1.0.0", "1.1.0"],
                        Authors = "PinkRooster",
                        Description = "Test package description",
                    }
                ),
        };
        var auditService = new MockVulnerabilityAuditService();
        var tools = new NugetDiscoveryTools(feedClient, auditService);

        CallToolResult result = await tools.nuget_get_metadata(
            "PinkRooster.Meta",
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("PinkRooster.Meta");
        textBlock.Text.Should().Contain("1.0.0");
        textBlock.Text.Should().Contain("1.1.0");
    }

    [Fact]
    public async Task NugetAuditVulnerabilities_WhenCalled_ReturnsAuditReport()
    {
        var feedClient = new MockFeedClient();
        var auditService = new MockVulnerabilityAuditService
        {
            AuditPackageHandler = (packageId, version, feedUrl) =>
                Task.FromResult(
                    new VulnerabilityAuditReport
                    {
                        PackageId = packageId,
                        AuditedVersion = version,
                        HasVulnerabilities = true,
                        TotalVulnerabilities = 1,
                        Versions =
                        [
                            new VersionVulnerabilityReport
                            {
                                Version = version ?? "1.0.0",
                                Vulnerabilities =
                                [
                                    new PackageVulnerabilityAdvisory
                                    {
                                        AdvisoryUrl = "https://github.com/advisories/GHSA-1234",
                                        Severity = "High",
                                    },
                                ],
                            },
                        ],
                    }
                ),
        };
        var tools = new NugetDiscoveryTools(feedClient, auditService);

        CallToolResult result = await tools.nuget_audit_vulnerabilities(
            "PinkRooster.Vulnerable",
            "1.0.0",
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("PinkRooster.Vulnerable");
        textBlock.Text.Should().Contain("GHSA-1234");
        textBlock.Text.Should().Contain("High");
    }

    [Fact]
    public void NugetInspectLocal_WhenPackageValid_ReturnsPackageArchiveInfo()
    {
        string nupkgPath = CreatePackage("PinkRooster.Inspect", "1.0.0");
        var feedClient = new MockFeedClient();
        var validator = new PublishSafetyValidator(feedClient);
        var tools = new NugetInspectionTools(validator);

        CallToolResult result = tools.nuget_inspect_local(nupkgPath);

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("PinkRooster.Inspect");
        textBlock.Text.Should().Contain("1.0.0");
    }

    [Fact]
    public async Task NugetValidatePublish_WhenValid_ReturnsSuccess()
    {
        string nupkgPath = CreatePackage("PinkRooster.ValidPublish", "1.0.0");
        var feedClient = new MockFeedClient
        {
            DoesVersionExistHandler = (_, _, _) => Task.FromResult(false),
        };
        var validator = new PublishSafetyValidator(feedClient);
        var tools = new NugetInspectionTools(validator);

        CallToolResult result = await tools.nuget_validate_publish(
            nupkgPath,
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("PinkRooster.ValidPublish");
    }

    [Fact]
    public async Task NugetValidatePublish_WhenCollision_ReturnsError()
    {
        string nupkgPath = CreatePackage("PinkRooster.Collision", "1.0.0");
        var feedClient = new MockFeedClient
        {
            DoesVersionExistHandler = (_, _, _) => Task.FromResult(true),
        };
        var validator = new PublishSafetyValidator(feedClient);
        var tools = new NugetInspectionTools(validator);

        CallToolResult result = await tools.nuget_validate_publish(
            nupkgPath,
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("PinkRooster.Collision");
        textBlock.Text.Should().Contain("VersionAlreadyExists");
    }

    [Fact]
    public async Task NugetPublish_WhenValid_PublishesPackageAndReturnsSuccess()
    {
        string nupkgPath = CreatePackage("PinkRooster.PublishTarget", "1.0.0");
        var publisherService = new MockPackagePublisherService
        {
            PublishHandler = req =>
                Task.FromResult(
                    new PublishPackageResponse
                    {
                        Success = true,
                        PackageId = "PinkRooster.PublishTarget",
                        Version = "1.0.0",
                        PublishedPackage = true,
                        StatusCode = 201,
                        Message = "Package successfully published.",
                    }
                ),
        };
        var tools = new NugetPublishingTools(publisherService);

        CallToolResult result = await tools.nuget_publish(
            nupkgPath,
            apiKey: "oy2a-valid-api-key-12345678901234567890",
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("PinkRooster.PublishTarget");
        textBlock.Text.Should().Contain("201");
    }

    [Fact]
    public async Task NugetPublish_WhenPublisherReportsFailure_ReturnsError()
    {
        string nupkgPath = CreatePackage("PinkRooster.FailPublish", "1.0.0");
        var publisherService = new MockPackagePublisherService
        {
            PublishHandler = req =>
                Task.FromResult(
                    new PublishPackageResponse
                    {
                        Success = false,
                        PackageId = "PinkRooster.FailPublish",
                        Version = "1.0.0",
                        PublishedPackage = false,
                        StatusCode = 409,
                        Message = "Package version already exists on the feed.",
                    }
                ),
        };
        var tools = new NugetPublishingTools(publisherService);

        CallToolResult result = await tools.nuget_publish(
            nupkgPath,
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("409");
        textBlock.Text.Should().Contain("already exists");
    }

    [Fact]
    public async Task NugetManageVersion_WhenUnlist_InvokesAdminServiceWithUnlist()
    {
        var adminService = new MockPackageAdministrationService
        {
            ManageVersionHandler = req =>
                Task.FromResult(
                    new ManageVersionResponse
                    {
                        Success = true,
                        PackageId = req.PackageId,
                        Version = req.Version,
                        Action = "unlist",
                        DryRun = req.DryRun,
                        PreviousStatus = "Listed",
                        CurrentStatus = "Unlisted",
                        Message = "Version was successfully unlisted.",
                    }
                ),
        };
        var tools = new NugetAdministrationTools(adminService);

        CallToolResult result = await tools.nuget_manage_version(
            "PinkRooster.Admin",
            "1.0.0",
            "unlist",
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("Unlisted");
    }

    [Fact]
    public async Task NugetManageVersion_WhenRelist_InvokesAdminServiceWithRelist()
    {
        var adminService = new MockPackageAdministrationService
        {
            ManageVersionHandler = req =>
                Task.FromResult(
                    new ManageVersionResponse
                    {
                        Success = true,
                        PackageId = req.PackageId,
                        Version = req.Version,
                        Action = "relist",
                        DryRun = req.DryRun,
                        PreviousStatus = "Unlisted",
                        CurrentStatus = "Listed",
                        Message = "Version was successfully relisted.",
                    }
                ),
        };
        var tools = new NugetAdministrationTools(adminService);

        CallToolResult result = await tools.nuget_manage_version(
            "PinkRooster.Admin",
            "1.0.0",
            "relist",
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("Listed");
    }

    [Fact]
    public async Task NugetManageVersion_WhenInvalidAction_ReturnsError()
    {
        var adminService = new MockPackageAdministrationService();
        var tools = new NugetAdministrationTools(adminService);

        CallToolResult result = await tools.nuget_manage_version(
            "PinkRooster.Admin",
            "1.0.0",
            "explode",
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().Contain("Invalid action 'explode'");
    }

    [Fact]
    public void NugetGetDeprecationUrl_WhenCalled_ReturnsUrlAndGuidance()
    {
        var adminService = new MockPackageAdministrationService
        {
            GetDeprecationPortalUrlHandler = req =>
                $"https://www.nuget.org/packages/{req.PackageId}/{req.Version}/deprecate?reasons={req.Reason}",
        };
        var tools = new NugetAdministrationTools(adminService);

        CallToolResult result = tools.nuget_get_deprecation_url(
            "PinkRooster.Old",
            "0.9.0",
            reason: "Legacy",
            alternatePackageId: "PinkRooster.New"
        );

        result.IsError.Should().BeFalse();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock
            .Text.Should()
            .Contain("https://www.nuget.org/packages/PinkRooster.Old/0.9.0/deprecate");
    }

    [Fact]
    public async Task NugetPublish_WhenExceptionThrownWithSecret_MasksSecretsInErrorResponse()
    {
        const string rawSecret = "oy2a-super-secret-key-12345678901234567890";
        var publisherService = new MockPackagePublisherService
        {
            PublishHandler = _ =>
                throw new InvalidOperationException($"Push failed with key {rawSecret}"),
        };
        var tools = new NugetPublishingTools(publisherService);

        CallToolResult result = await tools.nuget_publish(
            "any.nupkg",
            apiKey: rawSecret,
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().NotContain(rawSecret);
        textBlock.Text.Should().Contain("***REDACTED***");
    }

    [Fact]
    public async Task NugetManageVersion_WhenExceptionThrownWithSecret_MasksSecretsInErrorResponse()
    {
        const string rawSecret = "ghp_mockSecretPatToken12345678901234567890";
        var adminService = new MockPackageAdministrationService
        {
            ManageVersionHandler = _ =>
                throw new HttpRequestException($"Unauthorized: token {rawSecret}"),
        };
        var tools = new NugetAdministrationTools(adminService);

        CallToolResult result = await tools.nuget_manage_version(
            "Pkg",
            "1.0.0",
            "unlist",
            apiKey: rawSecret,
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().NotContain(rawSecret);
        textBlock.Text.Should().Contain("***REDACTED***");
    }

    [Fact]
    public async Task NugetDiscovery_WhenExceptionThrownWithSecret_MasksSecretsInErrorResponse()
    {
        const string rawSecret = "ghp_mockDiscoveryPatToken123456789012345678";
        var feedClient = new MockFeedClient
        {
            SearchPackagesHandler = (_, _, _, _) =>
                throw new InvalidOperationException($"Feed query failed: Bearer {rawSecret}"),
        };
        var auditService = new MockVulnerabilityAuditService();
        var tools = new NugetDiscoveryTools(feedClient, auditService);

        CallToolResult result = await tools.nuget_search(
            "Query",
            ct: TestContext.Current.CancellationToken
        );

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        var textBlock = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        textBlock.Text.Should().NotContain(rawSecret);
        textBlock.Text.Should().Contain("***REDACTED***");
    }

    [Fact]
    public void Program_CreateHostBuilder_RegistersAllServicesAndToolsInDI()
    {
        HostApplicationBuilder builder = Program.CreateHostBuilder();
        using IHost host = builder.Build();

        host.Services.GetRequiredService<HttpClient>().Should().NotBeNull();
        host.Services.GetRequiredService<INugetFeedClient>().Should().NotBeNull();
        host.Services.GetRequiredService<PublishSafetyValidator>().Should().NotBeNull();
        host.Services.GetRequiredService<IPackagePublisherService>().Should().NotBeNull();
        host.Services.GetRequiredService<IPackageAdministrationService>().Should().NotBeNull();
        host.Services.GetRequiredService<IVulnerabilityAuditService>().Should().NotBeNull();
        host.Services.GetRequiredService<NugetDiscoveryTools>().Should().NotBeNull();
        host.Services.GetRequiredService<NugetInspectionTools>().Should().NotBeNull();
        host.Services.GetRequiredService<NugetPublishingTools>().Should().NotBeNull();
        host.Services.GetRequiredService<NugetAdministrationTools>().Should().NotBeNull();
    }

    private string CreatePackage(string packageId, string version)
    {
        string nuspecContent = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{packageId}</id>
                <version>{version}</version>
                <authors>PinkRooster Team</authors>
                <description>A test package for MCP inspection.</description>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        string nupkgPath = Path.Combine(_tempDir, $"{packageId}.{version}.nupkg");
        using FileStream fs = File.Create(nupkgPath);
        using ZipArchive archive = new(fs, ZipArchiveMode.Create);
        ZipArchiveEntry entry = archive.CreateEntry($"{packageId}.nuspec");
        using Stream stream = entry.Open();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        writer.Write(nuspecContent);

        return nupkgPath;
    }

    private sealed class MockFeedClient : INugetFeedClient
    {
        public Func<
            string,
            int,
            bool,
            string?,
            Task<IReadOnlyList<PackageSearchResult>>
        >? SearchPackagesHandler { get; init; }
        public Func<
            PublishedPackagesQuery,
            Task<PublishedPackagesResult>
        >? ListPublishedPackagesHandler { get; init; }
        public Func<
            string,
            string?,
            Task<PackageMetadataResult>
        >? GetPackageMetadataHandler { get; init; }
        public Func<string, string, string?, Task<bool>>? DoesVersionExistHandler { get; init; }

        public Task<IReadOnlyList<PackageSearchResult>> SearchPackagesAsync(
            string query,
            int take = 20,
            bool prerelease = false,
            string? feedUrl = null,
            CancellationToken ct = default
        )
        {
            if (SearchPackagesHandler is not null)
            {
                return SearchPackagesHandler(query, take, prerelease, feedUrl);
            }
            return Task.FromResult<IReadOnlyList<PackageSearchResult>>([]);
        }

        public Task<PublishedPackagesResult> ListPublishedPackagesAsync(
            PublishedPackagesQuery query,
            CancellationToken ct = default
        )
        {
            if (ListPublishedPackagesHandler is not null)
            {
                return ListPublishedPackagesHandler(query);
            }
            return Task.FromResult(new PublishedPackagesResult());
        }

        public Task<PackageMetadataResult> GetPackageMetadataAsync(
            string packageId,
            string? feedUrl = null,
            CancellationToken ct = default
        )
        {
            if (GetPackageMetadataHandler is not null)
            {
                return GetPackageMetadataHandler(packageId, feedUrl);
            }
            return Task.FromResult(new PackageMetadataResult { PackageId = packageId });
        }

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

    private sealed class MockVulnerabilityAuditService : IVulnerabilityAuditService
    {
        public Func<
            string,
            string?,
            string?,
            Task<VulnerabilityAuditReport>
        >? AuditPackageHandler { get; init; }

        public Task<VulnerabilityAuditReport> AuditPackageAsync(
            string packageId,
            string? version = null,
            string? feedUrl = null,
            CancellationToken ct = default
        )
        {
            if (AuditPackageHandler is not null)
            {
                return AuditPackageHandler(packageId, version, feedUrl);
            }
            return Task.FromResult(
                new VulnerabilityAuditReport { PackageId = packageId, AuditedVersion = version }
            );
        }
    }

    private sealed class MockPackagePublisherService : IPackagePublisherService
    {
        public Func<
            PublishPackageRequest,
            Task<PublishPackageResponse>
        >? PublishHandler { get; init; }

        public Task<PublishPackageResponse> PublishAsync(
            PublishPackageRequest request,
            CancellationToken ct = default
        )
        {
            if (PublishHandler is not null)
            {
                return PublishHandler(request);
            }
            return Task.FromResult(new PublishPackageResponse { Success = true });
        }
    }

    private sealed class MockPackageAdministrationService : IPackageAdministrationService
    {
        public Func<
            ManageVersionRequest,
            Task<ManageVersionResponse>
        >? ManageVersionHandler { get; init; }
        public Func<DeprecationUrlRequest, string>? GetDeprecationPortalUrlHandler { get; init; }

        public Task<ManageVersionResponse> ManageVersionAsync(
            ManageVersionRequest request,
            CancellationToken ct = default
        )
        {
            if (ManageVersionHandler is not null)
            {
                return ManageVersionHandler(request);
            }
            return Task.FromResult(new ManageVersionResponse { Success = true });
        }

        public string GetDeprecationPortalUrl(DeprecationUrlRequest request)
        {
            if (GetDeprecationPortalUrlHandler is not null)
            {
                return GetDeprecationPortalUrlHandler(request);
            }
            return $"https://www.nuget.org/packages/{request.PackageId}/{request.Version}/deprecate";
        }
    }
}
