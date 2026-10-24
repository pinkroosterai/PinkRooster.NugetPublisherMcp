using System.Net;
using NuGet.Versioning;
using PinkRooster.NugetPublisher.Credentials;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Feeds.Models;

namespace PinkRooster.NugetPublisher.Administration;

/// <summary>
/// Provides package version administration operations for unlisting, relisting, and deprecation portal navigation.
/// </summary>
public sealed class PackageAdministrationService : IPackageAdministrationService
{
    private readonly INugetFeedClient _feedClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageAdministrationService"/> class using a default NuGet feed client.
    /// </summary>
    public PackageAdministrationService()
        : this(new NugetFeedClient()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageAdministrationService"/> class using the specified feed client.
    /// </summary>
    /// <param name="feedClient">The NuGet feed client instance to use for remote feed operations.</param>
    public PackageAdministrationService(INugetFeedClient feedClient)
    {
        ArgumentNullException.ThrowIfNull(feedClient);
        _feedClient = feedClient;
    }

    /// <inheritdoc />
    public async Task<ManageVersionResponse> ManageVersionAsync(
        ManageVersionRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PackageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Version);

        string effectiveFeedUrl = string.IsNullOrWhiteSpace(request.FeedUrl)
            ? CredentialResolver.DefaultFeedUrl
            : request.FeedUrl.Trim();

        CredentialResolver.RegisterActiveKey(request.ApiKey);

        string actionName = request.Action == ManageVersionAction.Unlist ? "unlist" : "relist";

        if (request.DryRun)
        {
            return await ExecuteDryRunAsync(request, effectiveFeedUrl, actionName, ct);
        }

        return request.Action switch
        {
            ManageVersionAction.Unlist => await ExecuteUnlistAsync(
                request,
                effectiveFeedUrl,
                actionName,
                ct
            ),
            ManageVersionAction.Relist => await ExecuteRelistAsync(
                request,
                effectiveFeedUrl,
                actionName,
                ct
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Action,
                "Unsupported version administration action."
            ),
        };
    }

    /// <inheritdoc />
    public string GetDeprecationPortalUrl(DeprecationUrlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PackageId);

        string escapedId = Uri.EscapeDataString(request.PackageId.Trim());
        string baseUrl = string.IsNullOrWhiteSpace(request.Version)
            ? $"https://www.nuget.org/packages/{escapedId}/deprecate"
            : $"https://www.nuget.org/packages/{escapedId}/{Uri.EscapeDataString(request.Version.Trim())}/deprecate";

        List<string> queryParams = [];
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            queryParams.Add($"reason={Uri.EscapeDataString(request.Reason.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(request.AlternatePackageId))
        {
            queryParams.Add(
                $"alternatePackageId={Uri.EscapeDataString(request.AlternatePackageId.Trim())}"
            );
        }

        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            queryParams.Add($"message={Uri.EscapeDataString(request.Message.Trim())}");
        }

        return queryParams.Count > 0 ? $"{baseUrl}?{string.Join("&", queryParams)}" : baseUrl;
    }

    private async Task<ManageVersionResponse> ExecuteDryRunAsync(
        ManageVersionRequest request,
        string effectiveFeedUrl,
        string actionName,
        CancellationToken ct
    )
    {
        bool packageExists = false;
        bool? isListed = null;

        try
        {
            PackageMetadataResult metadata = await _feedClient.GetPackageMetadataAsync(
                request.PackageId,
                effectiveFeedUrl,
                ct
            );

            if (metadata.Exists)
            {
                PackageVersionMetadata? match = metadata.VersionDetails.FirstOrDefault(v =>
                    string.Equals(v.Version, request.Version, StringComparison.OrdinalIgnoreCase)
                    || (
                        NuGetVersion.TryParse(v.Version, out NuGetVersion? v1)
                        && NuGetVersion.TryParse(request.Version, out NuGetVersion? v2)
                        && v1 == v2
                    )
                );

                if (match is not null)
                {
                    packageExists = true;
                    isListed = match.Listed;
                }
                else if (
                    metadata.Versions.Any(v =>
                        string.Equals(v, request.Version, StringComparison.OrdinalIgnoreCase)
                        || (
                            NuGetVersion.TryParse(v, out NuGetVersion? v1)
                            && NuGetVersion.TryParse(request.Version, out NuGetVersion? v2)
                            && v1 == v2
                        )
                    )
                )
                {
                    packageExists = true;
                    isListed = true;
                }
            }
        }
        catch (Exception)
        {
            // Fall back to existence check if metadata index cannot be queried
        }

        if (!packageExists)
        {
            bool exists = await _feedClient.DoesVersionExistAsync(
                request.PackageId,
                request.Version,
                effectiveFeedUrl,
                ct
            );

            if (exists)
            {
                packageExists = true;
                isListed = true;
            }
        }

        if (!packageExists)
        {
            return new ManageVersionResponse
            {
                Success = false,
                PackageId = request.PackageId,
                Version = request.Version,
                Action = actionName,
                DryRun = true,
                PreviousStatus = null,
                CurrentStatus = null,
                StatusCode = 404,
                Message = CredentialResolver.Sanitize(
                    $"Package '{request.PackageId}' version '{request.Version}' does not exist on feed '{effectiveFeedUrl}'.",
                    request.ApiKey
                )!,
            };
        }

        string prevStatus = isListed == false ? "Unlisted" : "Listed";
        string targetStatus =
            request.Action == ManageVersionAction.Unlist ? "Unlisted" : "Listed";
        string verb = request.Action == ManageVersionAction.Unlist ? "unlisted" : "relisted";
        string feedName = GetFeedDisplayName(effectiveFeedUrl);

        return new ManageVersionResponse
        {
            Success = true,
            PackageId = request.PackageId,
            Version = request.Version,
            Action = actionName,
            DryRun = true,
            PreviousStatus = prevStatus,
            CurrentStatus = targetStatus,
            StatusCode = 200,
            Message = CredentialResolver.Sanitize(
                $"[DryRun] Package '{request.PackageId}' version '{request.Version}' is currently {prevStatus} and would be {verb} on {feedName}.",
                request.ApiKey
            )!,
        };
    }

    private async Task<ManageVersionResponse> ExecuteUnlistAsync(
        ManageVersionRequest request,
        string effectiveFeedUrl,
        string actionName,
        CancellationToken ct
    )
    {
        try
        {
            using HttpResponseMessage response = await _feedClient.UnlistPackageAsync(
                request.PackageId,
                request.Version,
                effectiveFeedUrl,
                request.ApiKey,
                ct
            );

            return await HandleResponseAsync(
                response,
                request,
                effectiveFeedUrl,
                actionName,
                successPrevStatus: "Listed",
                successTargetStatus: "Unlisted",
                ct
            );
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.MethodNotAllowed)
        {
            return CreateMethodNotAllowedResponse(request, effectiveFeedUrl, actionName);
        }
    }

    private async Task<ManageVersionResponse> ExecuteRelistAsync(
        ManageVersionRequest request,
        string effectiveFeedUrl,
        string actionName,
        CancellationToken ct
    )
    {
        try
        {
            using HttpResponseMessage response = await _feedClient.RelistPackageAsync(
                request.PackageId,
                request.Version,
                effectiveFeedUrl,
                request.ApiKey,
                ct
            );

            return await HandleResponseAsync(
                response,
                request,
                effectiveFeedUrl,
                actionName,
                successPrevStatus: "Unlisted",
                successTargetStatus: "Listed",
                ct
            );
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.MethodNotAllowed)
        {
            return CreateMethodNotAllowedResponse(request, effectiveFeedUrl, actionName);
        }
    }

    private static async Task<ManageVersionResponse> HandleResponseAsync(
        HttpResponseMessage response,
        ManageVersionRequest request,
        string effectiveFeedUrl,
        string action,
        string successPrevStatus,
        string successTargetStatus,
        CancellationToken ct
    )
    {
        int statusCode = (int)response.StatusCode;
        string verb = action == "unlist" ? "unlisted" : "relisted";
        string feedName = GetFeedDisplayName(effectiveFeedUrl);

        if (response.IsSuccessStatusCode)
        {
            return new ManageVersionResponse
            {
                Success = true,
                PackageId = request.PackageId,
                Version = request.Version,
                Action = action,
                DryRun = false,
                PreviousStatus = successPrevStatus,
                CurrentStatus = successTargetStatus,
                StatusCode = statusCode,
                Message = CredentialResolver.Sanitize(
                    $"Package '{request.PackageId}' version '{request.Version}' was successfully {verb} on {feedName}.",
                    request.ApiKey
                )!,
            };
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ManageVersionResponse
            {
                Success = false,
                PackageId = request.PackageId,
                Version = request.Version,
                Action = action,
                DryRun = false,
                PreviousStatus = null,
                CurrentStatus = null,
                StatusCode = 404,
                Message = CredentialResolver.Sanitize(
                    $"Package '{request.PackageId}' version '{request.Version}' was not found on feed '{effectiveFeedUrl}'.",
                    request.ApiKey
                )!,
            };
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return new ManageVersionResponse
            {
                Success = false,
                PackageId = request.PackageId,
                Version = request.Version,
                Action = action,
                DryRun = false,
                PreviousStatus = null,
                CurrentStatus = null,
                StatusCode = 403,
                Message = CredentialResolver.Sanitize(
                    $"Access forbidden: Insufficient permissions or invalid API key to {action} package '{request.PackageId}' version '{request.Version}' on feed '{effectiveFeedUrl}'.",
                    request.ApiKey
                )!,
            };
        }

        if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
        {
            return CreateMethodNotAllowedResponse(request, effectiveFeedUrl, action);
        }

        string body = await response.Content.ReadAsStringAsync(ct);
        string fallback = !string.IsNullOrWhiteSpace(body)
            ? body
            : $"Failed to {action} package '{request.PackageId}' version '{request.Version}' with status code {statusCode} ({response.ReasonPhrase}).";

        return new ManageVersionResponse
        {
            Success = false,
            PackageId = request.PackageId,
            Version = request.Version,
            Action = action,
            DryRun = false,
            PreviousStatus = null,
            CurrentStatus = null,
            StatusCode = statusCode,
            Message = CredentialResolver.Sanitize(fallback, request.ApiKey)!,
        };
    }

    private static ManageVersionResponse CreateMethodNotAllowedResponse(
        ManageVersionRequest request,
        string effectiveFeedUrl,
        string action
    )
    {
        string httpMethod = action == "unlist" ? "DELETE" : "POST";
        string message =
            $"Feed '{effectiveFeedUrl}' returned 405 Method Not Allowed. The target feed does not support standard NuGet HTTP {httpMethod} unlisting or relisting (e.g. GitHub Packages 'nuget.pkg.github.com' does not support deleting or unlisting packages via NuGet V2 endpoint). Please use the feed's web portal or dedicated management API to administer this package version.";

        return new ManageVersionResponse
        {
            Success = false,
            PackageId = request.PackageId,
            Version = request.Version,
            Action = action,
            DryRun = false,
            PreviousStatus = null,
            CurrentStatus = null,
            StatusCode = 405,
            Message = CredentialResolver.Sanitize(message, request.ApiKey)!,
        };
    }

    private static string GetFeedDisplayName(string feedUrl) =>
        feedUrl.Contains("nuget.org", StringComparison.OrdinalIgnoreCase)
            ? "nuget.org"
            : $"feed '{feedUrl}'";
}
