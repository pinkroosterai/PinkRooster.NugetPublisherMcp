using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NuGet.Versioning;
using PinkRooster.NugetPublisher.Credentials;
using PinkRooster.NugetPublisher.Feeds.Models;
using PinkRooster.NugetPublisher.Packaging;

namespace PinkRooster.NugetPublisher.Feeds;

/// <summary>
/// Client implementation for interacting with NuGet v3 protocol feeds and V2-compatible push endpoints.
/// </summary>
public sealed class NugetFeedClient : INugetFeedClient, IDisposable
{
    private const string NugetProtocolVersion = "4.1.0";
    private const string NugetClientVersion = "6.13.2";
    private static readonly char[] TagSeparators = [' ', ',', ';'];
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly ConcurrentDictionary<string, FeedServiceEndpoints> _serviceEndpointCache = new(
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// Initializes a new instance of the <see cref="NugetFeedClient"/> class.
    /// </summary>
    /// <param name="httpClient">An optional custom <see cref="HttpClient"/> instance; if null, a default client is created.</param>
    public NugetFeedClient(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            _httpClient = CreateDefaultHttpClient();
            _disposeClient = true;
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        SocketsHttpHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
        };
        return new HttpClient(handler);
    }

    /// <summary>
    /// Clears any cached service index endpoint resolutions.
    /// </summary>
    public void ClearEndpointCache()
    {
        _serviceEndpointCache.Clear();
    }

    /// <inheritdoc />
    public async Task<FeedServiceEndpoints> GetServiceEndpointsAsync(
        string? feedUrl = null,
        CancellationToken ct = default
    )
    {
        string effectiveUrl = string.IsNullOrWhiteSpace(feedUrl)
            ? CredentialResolver.DefaultFeedUrl
            : feedUrl.Trim();

        if (_serviceEndpointCache.TryGetValue(effectiveUrl, out FeedServiceEndpoints? cached))
        {
            return cached;
        }

        // If the URL directly points to a V2 endpoint without index.json
        if (
            effectiveUrl.Contains("/v2", StringComparison.OrdinalIgnoreCase)
            && !effectiveUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        )
        {
            FeedServiceEndpoints v2Endpoints = new()
            {
                ServiceIndexUrl = effectiveUrl,
                PackagePublish = effectiveUrl,
                SymbolPackagePublish = null,
                RegistrationsBaseUrl = null,
                SearchQueryService = null,
                PackageBaseAddress = null,
                VulnerabilityInfo = null,
            };

            _serviceEndpointCache[effectiveUrl] = v2Endpoints;
            return v2Endpoints;
        }

        using HttpRequestMessage request = new(HttpMethod.Get, effectiveUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        ResolvedCredential credential = CredentialResolver.Resolve(effectiveUrl);
        ApplyCredentials(request, credential);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = await ParseJsonAsync(response, ct);

        string? packagePublish = null;
        string? symbolPackagePublish = null;
        string? registrationsBaseUrl = null;
        string? searchQueryService = null;
        string? packageBaseAddress = null;
        string? vulnerabilityInfo = null;

        if (
            doc.RootElement.TryGetProperty("resources", out JsonElement resources)
            && resources.ValueKind == JsonValueKind.Array
        )
        {
            // Evaluate resources prioritizing higher protocol versions
            foreach (JsonElement res in resources.EnumerateArray())
            {
                if (
                    !res.TryGetProperty("@type", out JsonElement typeProp)
                    || !res.TryGetProperty("@id", out JsonElement idProp)
                )
                {
                    continue;
                }

                string type = typeProp.GetString() ?? string.Empty;
                string id = idProp.GetString() ?? string.Empty;

                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                // PackagePublish
                if (type.Equals("PackagePublish/2.0.0", StringComparison.OrdinalIgnoreCase))
                {
                    packagePublish = id;
                }
                else if (
                    packagePublish is null
                    && type.StartsWith("PackagePublish", StringComparison.OrdinalIgnoreCase)
                )
                {
                    packagePublish = id;
                }

                // SymbolPackagePublish
                if (type.Equals("SymbolPackagePublish/4.9.0", StringComparison.OrdinalIgnoreCase))
                {
                    symbolPackagePublish = id;
                }
                else if (
                    symbolPackagePublish is null
                    && type.StartsWith("SymbolPackagePublish", StringComparison.OrdinalIgnoreCase)
                )
                {
                    symbolPackagePublish = id;
                }

                // RegistrationsBaseUrl
                if (type.Equals("RegistrationsBaseUrl/3.6.0", StringComparison.OrdinalIgnoreCase))
                {
                    registrationsBaseUrl = id;
                }
                else if (
                    registrationsBaseUrl is null
                    && (
                        type.Equals(
                            "RegistrationsBaseUrl/Versioned",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || type.Equals("RegistrationsBaseUrl", StringComparison.OrdinalIgnoreCase)
                        || type.StartsWith(
                            "RegistrationsBaseUrl",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                {
                    registrationsBaseUrl = id;
                }

                // SearchQueryService
                if (
                    type.Equals("SearchQueryService/3.0.0-rc", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("SearchQueryService", StringComparison.OrdinalIgnoreCase)
                )
                {
                    searchQueryService = id;
                }
                else if (
                    searchQueryService is null
                    && type.StartsWith("SearchQueryService", StringComparison.OrdinalIgnoreCase)
                )
                {
                    searchQueryService = id;
                }

                // PackageBaseAddress
                if (type.Equals("PackageBaseAddress/3.0.0", StringComparison.OrdinalIgnoreCase))
                {
                    packageBaseAddress = id;
                }
                else if (
                    packageBaseAddress is null
                    && type.StartsWith("PackageBaseAddress", StringComparison.OrdinalIgnoreCase)
                )
                {
                    packageBaseAddress = id;
                }

                // VulnerabilityInfo
                if (type.Equals("VulnerabilityInfo/6.7.0", StringComparison.OrdinalIgnoreCase))
                {
                    vulnerabilityInfo = id;
                }
                else if (
                    vulnerabilityInfo is null
                    && type.StartsWith("VulnerabilityInfo", StringComparison.OrdinalIgnoreCase)
                )
                {
                    vulnerabilityInfo = id;
                }
            }
        }

        FeedServiceEndpoints result = new()
        {
            ServiceIndexUrl = effectiveUrl,
            PackagePublish = packagePublish,
            SymbolPackagePublish = symbolPackagePublish,
            RegistrationsBaseUrl = registrationsBaseUrl,
            SearchQueryService = searchQueryService,
            PackageBaseAddress = packageBaseAddress,
            VulnerabilityInfo = vulnerabilityInfo,
        };

        _serviceEndpointCache[effectiveUrl] = result;
        return result;
    }

    /// <inheritdoc />
    public async Task<PackageMetadataResult> GetPackageMetadataAsync(
        string packageId,
        string? feedUrl = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        FeedServiceEndpoints endpoints = await GetServiceEndpointsAsync(feedUrl, ct);
        if (string.IsNullOrEmpty(endpoints.RegistrationsBaseUrl))
        {
            throw new InvalidOperationException(
                $"Target feed '{endpoints.ServiceIndexUrl}' does not expose a RegistrationsBaseUrl resource."
            );
        }

        string registrationUrl =
            $"{endpoints.RegistrationsBaseUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";

        using HttpRequestMessage request = new(HttpMethod.Get, registrationUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        ResolvedCredential credential = CredentialResolver.Resolve(endpoints.ServiceIndexUrl);
        ApplyCredentials(request, credential);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new PackageMetadataResult { PackageId = packageId, Exists = false };
        }

        response.EnsureSuccessStatusCode();

        using JsonDocument doc = await ParseJsonAsync(response, ct);

        List<PackageVersionMetadata> versionDetails = [];

        if (
            doc.RootElement.TryGetProperty("items", out JsonElement pages)
            && pages.ValueKind == JsonValueKind.Array
        )
        {
            foreach (JsonElement page in pages.EnumerateArray())
            {
                if (
                    page.TryGetProperty("items", out JsonElement pageItems)
                    && pageItems.ValueKind == JsonValueKind.Array
                )
                {
                    foreach (JsonElement leaf in pageItems.EnumerateArray())
                    {
                        if (
                            TryParsePackageVersionMetadata(leaf, out PackageVersionMetadata? pvm)
                            && pvm is not null
                        )
                        {
                            versionDetails.Add(pvm);
                        }
                    }
                }
                else if (
                    page.TryGetProperty("@id", out JsonElement pageUrlProp)
                    && pageUrlProp.ValueKind == JsonValueKind.String
                )
                {
                    // Fetch paged external items
                    string pageUrl = pageUrlProp.GetString()!;
                    using HttpRequestMessage pageReq = new(HttpMethod.Get, pageUrl);
                    pageReq.Headers.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json")
                    );
                    ApplyCredentials(pageReq, credential);

                    using HttpResponseMessage pageResp = await _httpClient.SendAsync(pageReq, ct);
                    if (pageResp.IsSuccessStatusCode)
                    {
                        using JsonDocument pageDoc = await ParseJsonAsync(pageResp, ct);
                        if (
                            pageDoc.RootElement.TryGetProperty(
                                "items",
                                out JsonElement extPageItems
                            )
                            && extPageItems.ValueKind == JsonValueKind.Array
                        )
                        {
                            foreach (JsonElement extLeaf in extPageItems.EnumerateArray())
                            {
                                if (
                                    TryParsePackageVersionMetadata(
                                        extLeaf,
                                        out PackageVersionMetadata? pvm
                                    ) && pvm is not null
                                )
                                {
                                    versionDetails.Add(pvm);
                                }
                            }
                        }
                    }
                }
            }
        }

        if (versionDetails.Count == 0)
        {
            return new PackageMetadataResult { PackageId = packageId, Exists = false };
        }

        // Sort versions by SemVer
        List<PackageVersionMetadata> sortedDetails =
        [
            .. versionDetails.OrderBy(
                v => v.Version,
                Comparer<string>.Create(
                    (v1, v2) =>
                    {
                        bool p1 = NuGetVersion.TryParse(v1, out NuGetVersion? nv1);
                        bool p2 = NuGetVersion.TryParse(v2, out NuGetVersion? nv2);
                        if (p1 && p2)
                        {
                            return nv1!.CompareTo(nv2);
                        }
                        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
                    }
                )
            ),
        ];

        PackageVersionMetadata latest = sortedDetails[^1];
        List<string> versions = [.. sortedDetails.Select(v => v.Version)];

        return new PackageMetadataResult
        {
            PackageId = packageId,
            LatestVersion = latest.Version,
            Authors = latest.Authors ?? string.Empty,
            Description = latest.Description ?? string.Empty,
            LicenseExpression = latest.LicenseExpression,
            LicenseUrl = latest.LicenseUrl,
            ProjectUrl = latest.ProjectUrl,
            Tags = latest.Tags,
            Versions = versions,
            VersionDetails = sortedDetails,
            DependencyGroups = latest.DependencyGroups,
            Exists = true,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageSearchResult>> SearchPackagesAsync(
        string query,
        int take = 20,
        bool prerelease = false,
        string? feedUrl = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        FeedServiceEndpoints endpoints = await GetServiceEndpointsAsync(feedUrl, ct);
        if (string.IsNullOrEmpty(endpoints.SearchQueryService))
        {
            throw new InvalidOperationException(
                $"Target feed '{endpoints.ServiceIndexUrl}' does not expose a SearchQueryService resource."
            );
        }

        string searchUrl =
            $"{endpoints.SearchQueryService.TrimEnd('/')}?q={Uri.EscapeDataString(query)}&take={take}&prerelease={prerelease.ToString().ToLowerInvariant()}&semVerLevel=2.0.0";

        using HttpRequestMessage request = new(HttpMethod.Get, searchUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        ResolvedCredential credential = CredentialResolver.Resolve(endpoints.ServiceIndexUrl);
        ApplyCredentials(request, credential);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = await ParseJsonAsync(response, ct);
        return ParsePackageSearchResults(doc.RootElement);
    }

    /// <inheritdoc />
    public async Task<PublishedPackagesResult> ListPublishedPackagesAsync(
        PublishedPackagesQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        FeedServiceEndpoints endpoints = await GetServiceEndpointsAsync(query.FeedUrl, ct);
        if (string.IsNullOrEmpty(endpoints.SearchQueryService))
        {
            throw new InvalidOperationException(
                $"Target feed '{endpoints.ServiceIndexUrl}' does not expose a SearchQueryService resource."
            );
        }

        List<string> terms = [];
        if (!string.IsNullOrWhiteSpace(query.Owner))
        {
            terms.Add($"owner:{query.Owner.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(query.Author))
        {
            terms.Add($"author:{query.Author.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            terms.Add(query.Query.Trim());
        }

        string queryString = string.Join(" ", terms);

        int take = query.Take > 0 ? query.Take : 20;
        int skip = query.Skip >= 0 ? query.Skip : 0;

        string searchUrl =
            $"{endpoints.SearchQueryService.TrimEnd('/')}?q={Uri.EscapeDataString(queryString)}&skip={skip}&take={take}&prerelease={query.Prerelease.ToString().ToLowerInvariant()}&semVerLevel=2.0.0";

        using HttpRequestMessage request = new(HttpMethod.Get, searchUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        ResolvedCredential credential = CredentialResolver.Resolve(endpoints.ServiceIndexUrl);
        ApplyCredentials(request, credential);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = await ParseJsonAsync(response, ct);

        int totalHits =
            doc.RootElement.TryGetProperty("totalHits", out JsonElement totalHitsEl)
            && totalHitsEl.TryGetInt32(out int th)
                ? th
                : 0;

        List<PackageSearchResult> packages = ParsePackageSearchResults(doc.RootElement);

        return new PublishedPackagesResult
        {
            TotalHits = totalHits,
            Owner = query.Owner,
            Author = query.Author,
            FeedUrl = endpoints.ServiceIndexUrl,
            Packages = packages,
        };
    }

    private static List<PackageSearchResult> ParsePackageSearchResults(JsonElement rootElement)
    {
        List<PackageSearchResult> results = [];

        if (
            rootElement.TryGetProperty("data", out JsonElement dataProp)
            && dataProp.ValueKind == JsonValueKind.Array
        )
        {
            foreach (JsonElement item in dataProp.EnumerateArray())
            {
                string id = item.TryGetProperty("id", out JsonElement idEl)
                    ? idEl.GetString() ?? string.Empty
                    : string.Empty;
                string version = item.TryGetProperty("version", out JsonElement verEl)
                    ? verEl.GetString() ?? string.Empty
                    : string.Empty;
                string description = item.TryGetProperty("description", out JsonElement descEl)
                    ? descEl.GetString() ?? string.Empty
                    : string.Empty;
                string? projectUrl = item.TryGetProperty("projectUrl", out JsonElement pUrlEl)
                    ? pUrlEl.GetString()
                    : null;

                long? totalDownloads =
                    item.TryGetProperty("totalDownloads", out JsonElement dlEl)
                    && dlEl.TryGetInt64(out long dl)
                        ? dl
                        : null;

                bool? verified =
                    item.TryGetProperty("verified", out JsonElement verifEl)
                    && (
                        verifEl.ValueKind == JsonValueKind.True
                        || verifEl.ValueKind == JsonValueKind.False
                    )
                        ? verifEl.GetBoolean()
                        : null;

                List<string> authors = ParseStringOrArray(item, "authors");
                List<string> owners = ParseStringOrArray(item, "owners");

                List<string> tags = [];
                if (item.TryGetProperty("tags", out JsonElement tagsEl))
                {
                    if (tagsEl.ValueKind == JsonValueKind.Array)
                    {
                        tags.AddRange(
                            tagsEl
                                .EnumerateArray()
                                .Select(t => t.GetString())
                                .Where(t => !string.IsNullOrEmpty(t))!
                        );
                    }
                    else if (tagsEl.ValueKind == JsonValueKind.String)
                    {
                        string s = tagsEl.GetString()!;
                        tags.AddRange(
                            s.Split(
                                TagSeparators,
                                StringSplitOptions.TrimEntries
                                    | StringSplitOptions.RemoveEmptyEntries
                            )
                        );
                    }
                }

                List<string> versions = [];
                if (
                    item.TryGetProperty("versions", out JsonElement versionsEl)
                    && versionsEl.ValueKind == JsonValueKind.Array
                )
                {
                    foreach (JsonElement vEl in versionsEl.EnumerateArray())
                    {
                        if (
                            vEl.TryGetProperty("version", out JsonElement innerVerEl)
                            && innerVerEl.GetString() is { } innerVer
                        )
                        {
                            versions.Add(innerVer);
                        }
                    }
                }

                results.Add(
                    new PackageSearchResult
                    {
                        Id = id,
                        Version = version,
                        Description = description,
                        Authors = authors,
                        Owners = owners,
                        Tags = tags,
                        ProjectUrl = projectUrl,
                        TotalDownloads = totalDownloads,
                        Verified = verified,
                        Versions = versions,
                    }
                );
            }
        }

        return results;
    }

    private static List<string> ParseStringOrArray(JsonElement element, string propertyName)
    {
        List<string> items = [];
        if (element.TryGetProperty(propertyName, out JsonElement prop))
        {
            if (prop.ValueKind == JsonValueKind.Array)
            {
                items.AddRange(
                    prop
                        .EnumerateArray()
                        .Select(x => x.GetString())
                        .Where(x => !string.IsNullOrEmpty(x))!
                );
            }
            else if (prop.ValueKind == JsonValueKind.String)
            {
                string s = prop.GetString()!;
                items.AddRange(
                    s.Split(
                        ',',
                        StringSplitOptions.TrimEntries
                            | StringSplitOptions.RemoveEmptyEntries
                    )
                );
            }
        }

        return items;
    }

    /// <inheritdoc />
    public async Task<bool> DoesVersionExistAsync(
        string packageId,
        string version,
        string? feedUrl = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        FeedServiceEndpoints endpoints = await GetServiceEndpointsAsync(feedUrl, ct);

        // 1. Direct query to version registration leaf if RegistrationsBaseUrl is available
        if (!string.IsNullOrEmpty(endpoints.RegistrationsBaseUrl))
        {
            string normalizedVersion = NuGetVersion.TryParse(version, out NuGetVersion? nv)
                ? nv.ToNormalizedString().ToLowerInvariant()
                : version.ToLowerInvariant();
            string leafUrl =
                $"{endpoints.RegistrationsBaseUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/{normalizedVersion}.json";

            using HttpRequestMessage leafRequest = new(HttpMethod.Get, leafUrl);
            leafRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            ResolvedCredential credential = CredentialResolver.Resolve(endpoints.ServiceIndexUrl);
            ApplyCredentials(leafRequest, credential);

            using HttpResponseMessage leafResponse = await _httpClient.SendAsync(leafRequest, ct);
            if (leafResponse.IsSuccessStatusCode)
            {
                return true;
            }
        }

        // 2. Fall back to package registration index check
        PackageMetadataResult metadata = await GetPackageMetadataAsync(packageId, feedUrl, ct);
        if (!metadata.Exists)
        {
            return false;
        }

        if (NuGetVersion.TryParse(version, out NuGetVersion? targetVersion))
        {
            return metadata.Versions.Any(v =>
                NuGetVersion.TryParse(v, out NuGetVersion? ev) && ev == targetVersion
            );
        }

        return metadata.Versions.Contains(version, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<PushResult> PushPackageAsync(
        Stream stream,
        string? feedUrl = null,
        string? apiKey = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        FeedServiceEndpoints endpoints = await GetServiceEndpointsAsync(feedUrl, ct);
        if (string.IsNullOrEmpty(endpoints.PackagePublish))
        {
            throw new InvalidOperationException(
                $"Target feed '{endpoints.ServiceIndexUrl}' does not expose a PackagePublish resource."
            );
        }

        string publishUrl = endpoints.PackagePublish;
        ResolvedCredential credential = CredentialResolver.Resolve(
            endpoints.ServiceIndexUrl,
            apiKey
        );

        using HttpRequestMessage request = new(HttpMethod.Put, publishUrl);
        using MultipartFormDataContent multipart = new();
        using StreamContent streamContent = new(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(streamContent, "package", "package.nupkg");
        request.Content = multipart;

        ApplyProtocolHeaders(request);
        ApplyCredentials(request, credential);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);
        string sanitizedMessage = BuildPushResultMessage(response, responseBody, apiKey);

        return new PushResult
        {
            Success = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            Message = sanitizedMessage,
            EndpointUrl = publishUrl,
        };
    }

    /// <inheritdoc />
    public async Task<PushResult> PushSymbolPackageAsync(
        Stream stream,
        string? symbolFeedUrl = null,
        string? apiKey = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        string? targetUrl;

        // If symbolFeedUrl is provided directly as a V2 publish endpoint
        if (
            !string.IsNullOrWhiteSpace(symbolFeedUrl)
            && !symbolFeedUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && symbolFeedUrl.Contains("/symbol", StringComparison.OrdinalIgnoreCase)
        )
        {
            targetUrl = symbolFeedUrl;
        }
        else
        {
            FeedServiceEndpoints endpoints = await GetServiceEndpointsAsync(symbolFeedUrl, ct);
            targetUrl = endpoints.SymbolPackagePublish;
        }

        if (string.IsNullOrEmpty(targetUrl))
        {
            return new PushResult
            {
                Success = false,
                StatusCode = 404,
                Message =
                    "Symbol publish endpoint (SymbolPackagePublish/4.9.0) not supported or found on target feed.",
                EndpointUrl = null,
            };
        }

        ResolvedCredential credential = CredentialResolver.Resolve(
            symbolFeedUrl ?? targetUrl,
            apiKey
        );

        using HttpRequestMessage request = new(HttpMethod.Put, targetUrl);
        using MultipartFormDataContent multipart = new();
        using StreamContent streamContent = new(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(streamContent, "package", "package.snupkg");
        request.Content = multipart;

        ApplyProtocolHeaders(request);
        ApplyCredentials(request, credential);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);
        string sanitizedMessage = BuildPushResultMessage(response, responseBody, apiKey);

        return new PushResult
        {
            Success = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            Message = sanitizedMessage,
            EndpointUrl = targetUrl,
        };
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> UnlistPackageAsync(
        string packageId,
        string version,
        string? feedUrl = null,
        string? apiKey = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        FeedServiceEndpoints endpoints = await GetServiceEndpointsAsync(feedUrl, ct);
        if (string.IsNullOrEmpty(endpoints.PackagePublish))
        {
            throw new InvalidOperationException(
                $"Target feed '{endpoints.ServiceIndexUrl}' does not expose a PackagePublish resource."
            );
        }

        string unlistUrl = $"{endpoints.PackagePublish.TrimEnd('/')}/{packageId}/{version}";
        ResolvedCredential credential = CredentialResolver.Resolve(
            endpoints.ServiceIndexUrl,
            apiKey
        );

        HttpRequestMessage request = new(HttpMethod.Delete, unlistUrl);
        ApplyProtocolHeaders(request);
        ApplyCredentials(request, credential);

        return await _httpClient.SendAsync(request, ct);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> RelistPackageAsync(
        string packageId,
        string version,
        string? feedUrl = null,
        string? apiKey = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        FeedServiceEndpoints endpoints = await GetServiceEndpointsAsync(feedUrl, ct);
        if (string.IsNullOrEmpty(endpoints.PackagePublish))
        {
            throw new InvalidOperationException(
                $"Target feed '{endpoints.ServiceIndexUrl}' does not expose a PackagePublish resource."
            );
        }

        string relistUrl = $"{endpoints.PackagePublish.TrimEnd('/')}/{packageId}/{version}";
        ResolvedCredential credential = CredentialResolver.Resolve(
            endpoints.ServiceIndexUrl,
            apiKey
        );

        HttpRequestMessage request = new(HttpMethod.Post, relistUrl);
        request.Content = new ByteArrayContent([]);
        ApplyProtocolHeaders(request);
        ApplyCredentials(request, credential);

        return await _httpClient.SendAsync(request, ct);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }

    private static void ApplyProtocolHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-NuGet-Protocol-Version", NugetProtocolVersion);
        request.Headers.TryAddWithoutValidation("X-NuGet-Client-Version", NugetClientVersion);
    }

    private static string BuildPushResultMessage(
        HttpResponseMessage response,
        string responseBody,
        string? apiKey
    )
    {
        string detailedBody;
        if (response.IsSuccessStatusCode)
        {
            detailedBody = !string.IsNullOrWhiteSpace(responseBody)
                ? responseBody
                : "Package pushed successfully.";
        }
        else
        {
            string reasonPhrase = response.ReasonPhrase?.Trim() ?? string.Empty;
            string body = responseBody.Trim();

            if (string.IsNullOrWhiteSpace(body))
            {
                detailedBody = !string.IsNullOrWhiteSpace(reasonPhrase)
                    ? reasonPhrase
                    : $"Push failed with status code {(int)response.StatusCode}.";
            }
            else if (
                string.IsNullOrWhiteSpace(reasonPhrase)
                || body.Contains(reasonPhrase, StringComparison.OrdinalIgnoreCase)
            )
            {
                detailedBody = body;
            }
            else if (reasonPhrase.Contains(body, StringComparison.OrdinalIgnoreCase))
            {
                detailedBody = reasonPhrase;
            }
            else
            {
                detailedBody = $"{reasonPhrase}: {body}";
            }
        }

        return CredentialResolver.Sanitize(detailedBody, apiKey);
    }

    private static void ApplyCredentials(HttpRequestMessage request, ResolvedCredential credential)
    {
        if (
            string.IsNullOrEmpty(credential.HeaderName)
            || string.IsNullOrEmpty(credential.HeaderValue)
        )
        {
            return;
        }

        if (
            string.Equals(
                credential.HeaderName,
                "Authorization",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            string val = credential.HeaderValue.Trim();
            if (val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    val["Bearer ".Length..].Trim()
                );
            }
            else if (val.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    val["Basic ".Length..].Trim()
                );
            }
            else
            {
                request.Headers.TryAddWithoutValidation("Authorization", val);
            }
        }
        else
        {
            request.Headers.TryAddWithoutValidation(credential.HeaderName, credential.HeaderValue);
        }
    }

    private static bool TryParsePackageVersionMetadata(
        JsonElement leaf,
        out PackageVersionMetadata? metadata
    )
    {
        metadata = null;

        JsonElement entry = leaf;
        if (
            leaf.TryGetProperty("catalogEntry", out JsonElement catalogEntry)
            && catalogEntry.ValueKind == JsonValueKind.Object
        )
        {
            entry = catalogEntry;
        }

        if (!entry.TryGetProperty("version", out JsonElement verEl))
        {
            return false;
        }

        string version = verEl.GetString() ?? string.Empty;
        if (string.IsNullOrEmpty(version))
        {
            return false;
        }

        bool listed = true;
        if (
            entry.TryGetProperty("listed", out JsonElement listedEl)
            && (
                listedEl.ValueKind == JsonValueKind.True
                || listedEl.ValueKind == JsonValueKind.False
            )
        )
        {
            listed = listedEl.GetBoolean();
        }

        DateTimeOffset? published = null;
        if (
            entry.TryGetProperty("published", out JsonElement pubEl)
            && pubEl.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(pubEl.GetString(), out DateTimeOffset dt)
        )
        {
            published = dt;
        }

        string? description = entry.TryGetProperty("description", out JsonElement descEl)
            ? descEl.GetString()
            : null;

        string? authors = null;
        if (entry.TryGetProperty("authors", out JsonElement authEl))
        {
            if (authEl.ValueKind == JsonValueKind.String)
            {
                authors = authEl.GetString();
            }
            else if (authEl.ValueKind == JsonValueKind.Array)
            {
                authors = string.Join(
                    ", ",
                    authEl
                        .EnumerateArray()
                        .Select(a => a.GetString())
                        .Where(a => !string.IsNullOrEmpty(a))
                );
            }
        }

        string? licenseExpression = entry.TryGetProperty("licenseExpression", out JsonElement licEl)
            ? licEl.GetString()
            : null;

        string? licenseUrl = entry.TryGetProperty("licenseUrl", out JsonElement licUrlEl)
            ? licUrlEl.GetString()
            : null;

        string? summary = entry.TryGetProperty("summary", out JsonElement sumEl)
            ? sumEl.GetString()
            : null;

        string? projectUrl = entry.TryGetProperty("projectUrl", out JsonElement pUrlEl)
            ? pUrlEl.GetString()
            : null;

        string? packageContent = leaf.TryGetProperty("packageContent", out JsonElement contentEl)
            ? contentEl.GetString()
            : null;

        List<string> tags = [];
        if (entry.TryGetProperty("tags", out JsonElement tagsEl))
        {
            if (tagsEl.ValueKind == JsonValueKind.Array)
            {
                tags.AddRange(
                    tagsEl
                        .EnumerateArray()
                        .Select(t => t.GetString())
                        .Where(t => !string.IsNullOrEmpty(t))!
                );
            }
            else if (tagsEl.ValueKind == JsonValueKind.String)
            {
                string s = tagsEl.GetString()!;
                tags.AddRange(
                    s.Split(
                        TagSeparators,
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                    )
                );
            }
        }

        List<PackageDependencyGroupInfo> dependencyGroups = [];
        if (
            entry.TryGetProperty("dependencyGroups", out JsonElement groupsEl)
            && groupsEl.ValueKind == JsonValueKind.Array
        )
        {
            foreach (JsonElement groupEl in groupsEl.EnumerateArray())
            {
                string? targetFramework = groupEl.TryGetProperty(
                    "targetFramework",
                    out JsonElement tfmEl
                )
                    ? tfmEl.GetString()
                    : null;

                List<PackageDependencyInfo> dependencies = [];
                if (
                    groupEl.TryGetProperty("dependencies", out JsonElement depsEl)
                    && depsEl.ValueKind == JsonValueKind.Array
                )
                {
                    foreach (JsonElement depEl in depsEl.EnumerateArray())
                    {
                        string id = depEl.TryGetProperty("id", out JsonElement depIdEl)
                            ? depIdEl.GetString() ?? string.Empty
                            : string.Empty;
                        string range = depEl.TryGetProperty("range", out JsonElement depRangeEl)
                            ? depRangeEl.GetString() ?? string.Empty
                            : string.Empty;

                        if (!string.IsNullOrEmpty(id))
                        {
                            dependencies.Add(new PackageDependencyInfo(id, range, targetFramework));
                        }
                    }
                }

                dependencyGroups.Add(
                    new PackageDependencyGroupInfo
                    {
                        TargetFramework = targetFramework,
                        Dependencies = dependencies,
                    }
                );
            }
        }

        List<PackageVulnerabilityMetadata> vulnerabilities = [];
        if (
            entry.TryGetProperty("vulnerabilities", out JsonElement vulnsEl)
            && vulnsEl.ValueKind == JsonValueKind.Array
        )
        {
            foreach (JsonElement vulnEl in vulnsEl.EnumerateArray())
            {
                string advUrl = vulnEl.TryGetProperty("advisoryUrl", out JsonElement advEl)
                    ? advEl.GetString() ?? string.Empty
                    : string.Empty;
                string severity = vulnEl.TryGetProperty("severity", out JsonElement sevEl)
                    ? sevEl.GetString() ?? string.Empty
                    : string.Empty;
                string? vulnDescription = vulnEl.TryGetProperty(
                    "description",
                    out JsonElement descVulnEl
                )
                    ? descVulnEl.GetString()
                    : (
                        vulnEl.TryGetProperty("summary", out JsonElement sumVulnEl)
                            ? sumVulnEl.GetString()
                            : null
                    );

                if (!string.IsNullOrEmpty(advUrl))
                {
                    vulnerabilities.Add(
                        new PackageVulnerabilityMetadata
                        {
                            AdvisoryUrl = advUrl,
                            Severity = severity,
                            Description = vulnDescription,
                        }
                    );
                }
            }
        }

        PackageDeprecationMetadata? deprecation = null;
        if (
            entry.TryGetProperty("deprecation", out JsonElement deprecationEl)
            && deprecationEl.ValueKind == JsonValueKind.Object
        )
        {
            List<string> reasons = [];
            if (
                deprecationEl.TryGetProperty("reasons", out JsonElement reasonsEl)
                && reasonsEl.ValueKind == JsonValueKind.Array
            )
            {
                foreach (JsonElement r in reasonsEl.EnumerateArray())
                {
                    string? reason = r.GetString();
                    if (!string.IsNullOrEmpty(reason))
                    {
                        reasons.Add(reason);
                    }
                }
            }

            string? message = deprecationEl.TryGetProperty("message", out JsonElement msgEl)
                ? msgEl.GetString()
                : null;
            string? alternatePackage = null;
            if (
                deprecationEl.TryGetProperty("alternatePackage", out JsonElement altPkgEl)
                && altPkgEl.ValueKind == JsonValueKind.Object
            )
            {
                alternatePackage = altPkgEl.TryGetProperty("id", out JsonElement altIdEl)
                    ? altIdEl.GetString()
                    : null;
            }

            deprecation = new PackageDeprecationMetadata
            {
                Reasons = reasons,
                Message = message,
                AlternatePackageId = alternatePackage,
            };
        }

        metadata = new PackageVersionMetadata
        {
            Version = version,
            Listed = listed,
            Published = published,
            Description = description,
            Authors = authors,
            LicenseExpression = licenseExpression,
            LicenseUrl = licenseUrl,
            Summary = summary,
            ProjectUrl = projectUrl,
            PackageContent = packageContent,
            Tags = tags,
            DependencyGroups = dependencyGroups,
            Vulnerabilities = vulnerabilities,
            Deprecation = deprecation,
        };

        return true;
    }

    private static async Task<JsonDocument> ParseJsonAsync(
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);

        if (
            (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            || response.Content.Headers.ContentEncoding.Contains(
                "gzip",
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            using MemoryStream raw = new(bytes);
            using GZipStream gz = new(raw, CompressionMode.Decompress);
            using MemoryStream decompressed = new();
            await gz.CopyToAsync(decompressed, ct);
            decompressed.Position = 0;
            return await JsonDocument.ParseAsync(decompressed, cancellationToken: ct);
        }

        if (
            response.Content.Headers.ContentEncoding.Contains(
                "br",
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            using MemoryStream raw = new(bytes);
            using BrotliStream br = new(raw, CompressionMode.Decompress);
            using MemoryStream decompressed = new();
            await br.CopyToAsync(decompressed, ct);
            decompressed.Position = 0;
            return await JsonDocument.ParseAsync(decompressed, cancellationToken: ct);
        }

        if (
            response.Content.Headers.ContentEncoding.Contains(
                "deflate",
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            using MemoryStream raw = new(bytes);
            using DeflateStream def = new(raw, CompressionMode.Decompress);
            using MemoryStream decompressed = new();
            await def.CopyToAsync(decompressed, ct);
            decompressed.Position = 0;
            return await JsonDocument.ParseAsync(decompressed, cancellationToken: ct);
        }

        return JsonDocument.Parse(bytes);
    }
}
