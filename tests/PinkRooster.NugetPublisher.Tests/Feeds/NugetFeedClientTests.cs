using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using PinkRooster.NugetPublisher.Credentials;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Feeds.Models;

namespace PinkRooster.NugetPublisher.Tests.Feeds;

public sealed class NugetFeedClientTests : IDisposable
{
    private const string ServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private const string PackagePublishUrl = "https://www.nuget.org/api/v2/package";
    private const string SymbolPublishUrl = "https://www.nuget.org/api/v2/symbolpackage";
    private const string RegistrationsBaseUrl = "https://api.nuget.org/v3/registration5-semver1/";
    private const string SearchServiceUrl = "https://azuresearch-usnc.nuget.org/query";

    private const string SampleServiceIndexJson = $$"""
        {
          "version": "3.0.0",
          "resources": [
            {
              "@id": "{{PackagePublishUrl}}",
              "@type": "PackagePublish/2.0.0"
            },
            {
              "@id": "{{SymbolPublishUrl}}",
              "@type": "SymbolPackagePublish/4.9.0"
            },
            {
              "@id": "{{RegistrationsBaseUrl}}",
              "@type": "RegistrationsBaseUrl/3.6.0"
            },
            {
              "@id": "{{SearchServiceUrl}}",
              "@type": "SearchQueryService"
            }
          ]
        }
        """;

    public NugetFeedClientTests()
    {
        CredentialResolver.ClearActiveKeys();
    }

    public void Dispose()
    {
        CredentialResolver.ClearActiveKeys();
    }

    [Fact]
    public async Task GetServiceEndpointsAsync_DiscoversEndpointsFromIndexAndCachesResult()
    {
        // Arrange
        int requestCount = 0;
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString() == ServiceIndexUrl)
            {
                Interlocked.Increment(ref requestCount);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);

        // Act
        FeedServiceEndpoints endpoints1 = await client.GetServiceEndpointsAsync(
            ServiceIndexUrl,
            TestContext.Current.CancellationToken
        );
        FeedServiceEndpoints endpoints2 = await client.GetServiceEndpointsAsync(
            ServiceIndexUrl,
            TestContext.Current.CancellationToken
        );

        // Assert
        endpoints1.PackagePublish.Should().Be(PackagePublishUrl);
        endpoints1.SymbolPackagePublish.Should().Be(SymbolPublishUrl);
        endpoints1.RegistrationsBaseUrl.Should().Be(RegistrationsBaseUrl);
        endpoints1.SearchQueryService.Should().Be(SearchServiceUrl);
        requestCount
            .Should()
            .Be(1, "the service index endpoints should be cached across multiple calls");
    }

    [Fact]
    public async Task GetPackageMetadataAsync_ParsesRegistrationIndexAndVersionDetails()
    {
        // Arrange
        const string registrationJson = """
            {
              "count": 1,
              "items": [
                {
                  "@id": "https://api.nuget.org/v3/registration5-semver1/test.package/page/1.0.0/1.1.0.json",
                  "count": 2,
                  "lower": "1.0.0",
                  "upper": "1.1.0",
                  "items": [
                    {
                      "@id": "https://api.nuget.org/v3/registration5-semver1/test.package/1.0.0.json",
                      "catalogEntry": {
                        "id": "Test.Package",
                        "version": "1.0.0",
                        "authors": "Author A",
                        "description": "First version",
                        "licenseExpression": "MIT",
                        "projectUrl": "https://example.com/proj",
                        "listed": true,
                        "published": "2025-01-01T00:00:00Z",
                        "tags": [ "test", "v1" ],
                        "dependencyGroups": [
                          {
                            "targetFramework": "net10.0",
                            "dependencies": [
                              {
                                "id": "System.Text.Json",
                                "range": "[10.0.0, )"
                              }
                            ]
                          }
                        ]
                      }
                    },
                    {
                      "@id": "https://api.nuget.org/v3/registration5-semver1/test.package/1.1.0.json",
                      "catalogEntry": {
                        "id": "Test.Package",
                        "version": "1.1.0",
                        "authors": "Author A, Author B",
                        "description": "Second version",
                        "licenseExpression": "Apache-2.0",
                        "projectUrl": "https://example.com/proj",
                        "listed": true,
                        "published": "2025-06-01T00:00:00Z",
                        "tags": "test v2 multi",
                        "dependencyGroups": [],
                        "vulnerabilities": [
                          {
                            "advisoryUrl": "https://github.com/advisories/GHSA-1234",
                            "severity": "High"
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var handler = new MockHttpMessageHandler(req =>
        {
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url == $"{RegistrationsBaseUrl}test.package/index.json")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        registrationJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);

        // Act
        PackageMetadataResult metadata = await client.GetPackageMetadataAsync(
            "Test.Package",
            ServiceIndexUrl,
            TestContext.Current.CancellationToken
        );

        // Assert
        metadata.Exists.Should().BeTrue();
        metadata.PackageId.Should().Be("Test.Package");
        metadata.Versions.Should().HaveCount(2);
        metadata.Versions.Should().Contain("1.0.0");
        metadata.Versions.Should().Contain("1.1.0");
        metadata.LatestVersion.Should().Be("1.1.0");
        metadata.Description.Should().Be("Second version");
        metadata.Authors.Should().Be("Author A, Author B");
        metadata.LicenseExpression.Should().Be("Apache-2.0");

        metadata.VersionDetails.Should().HaveCount(2);
        PackageVersionMetadata v1 = metadata.VersionDetails.First(v => v.Version == "1.0.0");
        v1.Listed.Should().BeTrue();
        v1.DependencyGroups.Should().HaveCount(1);
        v1.DependencyGroups[0].TargetFramework.Should().Be("net10.0");
        v1.DependencyGroups[0].Dependencies.Should().HaveCount(1);
        v1.DependencyGroups[0].Dependencies[0].Id.Should().Be("System.Text.Json");
        v1.DependencyGroups[0].Dependencies[0].VersionRange.Should().Be("[10.0.0, )");

        PackageVersionMetadata v2 = metadata.VersionDetails.First(v => v.Version == "1.1.0");
        v2.Vulnerabilities.Should().HaveCount(1);
        v2.Vulnerabilities[0].AdvisoryUrl.Should().Be("https://github.com/advisories/GHSA-1234");
        v2.Vulnerabilities[0].Severity.Should().Be("High");
    }

    [Fact]
    public async Task GetPackageMetadataAsync_WhenPackageNotFound_ReturnsExistsFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(req =>
        {
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);

        // Act
        PackageMetadataResult metadata = await client.GetPackageMetadataAsync(
            "NonExistent.Package",
            ServiceIndexUrl,
            TestContext.Current.CancellationToken
        );

        // Assert
        metadata.Exists.Should().BeFalse();
        metadata.PackageId.Should().Be("NonExistent.Package");
        metadata.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesVersionExistAsync_WhenLeafReturnsOk_ReturnsTrue()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(req =>
        {
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url == $"{RegistrationsBaseUrl}test.package/1.2.3.json")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"listed\": true}",
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);

        // Act
        bool exists = await client.DoesVersionExistAsync(
            "Test.Package",
            "1.2.3",
            ServiceIndexUrl,
            TestContext.Current.CancellationToken
        );

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DoesVersionExistAsync_WhenVersionMissing_ReturnsFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(req =>
        {
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);

        // Act
        bool exists = await client.DoesVersionExistAsync(
            "Test.Package",
            "9.9.9",
            ServiceIndexUrl,
            TestContext.Current.CancellationToken
        );

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task SearchPackagesAsync_QueriesSearchServiceAndParsesResults()
    {
        // Arrange
        const string searchResponseJson = """
            {
              "totalHits": 1,
              "data": [
                {
                  "id": "PinkRooster.NugetPublisher",
                  "version": "1.0.0",
                  "description": "Core publish engine",
                  "authors": [ "Jan Versteeg" ],
                  "tags": [ "nuget", "publisher" ],
                  "projectUrl": "https://github.com/pinkroosterai/PinkRooster.NugetPublisherMcp",
                  "totalDownloads": 4200,
                  "verified": true,
                  "versions": [
                    { "version": "1.0.0" }
                  ]
                }
              ]
            }
            """;

        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url.StartsWith(SearchServiceUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        searchResponseJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);

        // Act
        IReadOnlyList<PackageSearchResult> results = await client.SearchPackagesAsync(
            query: "PinkRooster",
            take: 10,
            prerelease: true,
            feedUrl: ServiceIndexUrl,
            ct: TestContext.Current.CancellationToken
        );

        // Assert
        results.Should().HaveCount(1);
        PackageSearchResult item = results[0];
        item.Id.Should().Be("PinkRooster.NugetPublisher");
        item.Version.Should().Be("1.0.0");
        item.Description.Should().Be("Core publish engine");
        item.Authors.Should().ContainSingle().Which.Should().Be("Jan Versteeg");
        item.Tags.Should().Contain("nuget");
        item.TotalDownloads.Should().Be(4200);
        item.Verified.Should().Be(true);
        item.Versions.Should().Contain("1.0.0");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.Query.Should().Contain("q=PinkRooster");
        capturedRequest.RequestUri.Query.Should().Contain("take=10");
        capturedRequest.RequestUri.Query.Should().Contain("prerelease=true");
    }

    [Fact]
    public async Task PushPackageAsync_SendsMultipartFormDataWithApiKeyHeader()
    {
        // Arrange
        const string apiKey = "oy2_test_api_key_12345678901234567890";
        HttpRequestMessage? capturedRequest = null;
        List<HttpContent>? capturedParts = null;
        byte[] packageBytes = [1, 2, 3, 4, 5];

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            if (req.Content is MultipartFormDataContent mp)
            {
                capturedParts = [.. mp];
            }

            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url == PackagePublishUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(
                        "Package pushed successfully.",
                        Encoding.UTF8,
                        "text/plain"
                    ),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);
        using var packageStream = new MemoryStream(packageBytes);

        // Act
        PushResult result = await client.PushPackageAsync(
            packageStream,
            ServiceIndexUrl,
            apiKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.EndpointUrl.Should().Be(PackagePublishUrl);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Put);
        capturedRequest.RequestUri?.ToString().Should().Be(PackagePublishUrl);
        capturedRequest.Headers.Contains("X-NuGet-ApiKey").Should().BeTrue();
        capturedRequest.Headers.GetValues("X-NuGet-ApiKey").First().Should().Be(apiKey);

        capturedParts.Should().NotBeNull();
        HttpContent part = capturedParts!.First();
        part.Headers.ContentDisposition.Should().NotBeNull();
        part.Headers.ContentDisposition!.Name.Should().Be("package");
        part.Headers.ContentDisposition.FileName.Should().Be("package.nupkg");
    }

    [Fact]
    public async Task PushPackageAsync_WhenBearerToken_InjectsAuthorizationHeader()
    {
        // Arrange
        const string bearerToken = "Bearer secret_jwt_token_1234567890";
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url == PackagePublishUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        PushResult result = await client.PushPackageAsync(
            stream,
            ServiceIndexUrl,
            bearerToken,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("secret_jwt_token_1234567890");
        capturedRequest.Headers.Contains("X-NuGet-ApiKey").Should().BeFalse();
    }

    [Fact]
    public async Task PushPackageAsync_WhenConflict_ReturnsFailureWithSanitizedMessage()
    {
        // Arrange
        const string secretKey = "oy2_secret_key_12345678901234567890";
        string serverResponse =
            $"Cannot upload package with key {secretKey}: version 1.0.0 already exists.";

        var handler = new MockHttpMessageHandler(req =>
        {
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url == PackagePublishUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent(serverResponse, Encoding.UTF8, "text/plain"),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        PushResult result = await client.PushPackageAsync(
            stream,
            ServiceIndexUrl,
            secretKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().NotContain(secretKey);
        result.Message.Should().Contain(CredentialResolver.RedactedPlaceholder);
    }

    [Fact]
    public async Task PushSymbolPackageAsync_SendsMultipartToSymbolEndpoint()
    {
        // Arrange
        const string apiKey = "oy2_symbol_test_key_1234567890123456";
        HttpRequestMessage? capturedRequest = null;
        List<HttpContent>? capturedParts = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            if (req.Content is MultipartFormDataContent mp)
            {
                capturedParts = [.. mp];
            }

            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url == SymbolPublishUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        PushResult result = await client.PushSymbolPackageAsync(
            stream,
            ServiceIndexUrl,
            apiKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.EndpointUrl.Should().Be(SymbolPublishUrl);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Put);
        capturedRequest.RequestUri?.ToString().Should().Be(SymbolPublishUrl);
        capturedRequest.Headers.Contains("X-NuGet-ApiKey").Should().BeTrue();

        capturedParts.Should().NotBeNull();
        HttpContent part = capturedParts!.First();
        part.Headers.ContentDisposition!.FileName.Should().Be("package.snupkg");
    }

    [Fact]
    public async Task UnlistPackageAsync_SendsDeleteToPackagePublishEndpoint()
    {
        // Arrange
        const string apiKey = "oy2_unlist_key_12345678901234567890";
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url == $"{PackagePublishUrl}/Test.Package/1.0.0")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);

        // Act
        using HttpResponseMessage response = await client.UnlistPackageAsync(
            "Test.Package",
            "1.0.0",
            ServiceIndexUrl,
            apiKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Delete);
        capturedRequest
            .RequestUri?.ToString()
            .Should()
            .Be($"{PackagePublishUrl}/Test.Package/1.0.0");
        capturedRequest.Headers.GetValues("X-NuGet-ApiKey").First().Should().Be(apiKey);
    }

    [Fact]
    public async Task RelistPackageAsync_SendsPostToPackagePublishEndpoint()
    {
        // Arrange
        const string apiKey = "oy2_relist_key_12345678901234567890";
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url == $"{PackagePublishUrl}/Test.Package/1.0.0")
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);

        // Act
        using HttpResponseMessage response = await client.RelistPackageAsync(
            "Test.Package",
            "1.0.0",
            ServiceIndexUrl,
            apiKey,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest
            .RequestUri?.ToString()
            .Should()
            .Be($"{PackagePublishUrl}/Test.Package/1.0.0");
        capturedRequest.Headers.GetValues("X-NuGet-ApiKey").First().Should().Be(apiKey);
    }

    [Fact]
    public async Task GetPackageMetadataAsync_WhenFeedReturnsGzipContent_SuccessfullyDecompressesAndParses()
    {
        // Arrange
        const string packageId = "Test.GzipPackage";
        const string rawRegistrationJson = """
            {
              "items": [
                {
                  "items": [
                    {
                      "catalogEntry": {
                        "version": "1.0.0",
                        "listed": true,
                        "description": "Gzipped metadata test package",
                        "authors": "PinkRooster"
                      }
                    }
                  ]
                }
              ]
            }
            """;

        byte[] rawBytes = Encoding.UTF8.GetBytes(rawRegistrationJson);
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
        {
            gz.Write(rawBytes, 0, rawBytes.Length);
        }
        byte[] gzippedBytes = ms.ToArray();

        var handler = new MockHttpMessageHandler(req =>
        {
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (url == ServiceIndexUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SampleServiceIndexJson,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
            if (url == $"{RegistrationsBaseUrl}{packageId.ToLowerInvariant()}/index.json")
            {
                var content = new ByteArrayContent(gzippedBytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                content.Headers.ContentEncoding.Add("gzip");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content,
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new NugetFeedClient(httpClient);

        // Act
        PackageMetadataResult result = await client.GetPackageMetadataAsync(
            packageId,
            ServiceIndexUrl,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.Exists.Should().BeTrue();
        result.PackageId.Should().Be(packageId);
        result.Versions.Should().Contain("1.0.0");
        result.Description.Should().Be("Gzipped metadata test package");
        result.VersionDetails.Should().HaveCount(1);
        result.VersionDetails[0].Version.Should().Be("1.0.0");
    }
}

/// <summary>
/// Simple mock <see cref="HttpMessageHandler"/> for intercepting HTTP client requests in tests.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _syncHandler;
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>>? _asyncHandler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _syncHandler = handler;
        _asyncHandler = null;
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _syncHandler = req => handler(req).GetAwaiter().GetResult();
        _asyncHandler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (_asyncHandler is not null)
        {
            return await _asyncHandler(request);
        }

        return _syncHandler(request);
    }
}
