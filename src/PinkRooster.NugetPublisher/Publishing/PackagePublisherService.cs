using PinkRooster.NugetPublisher.Credentials;
using PinkRooster.NugetPublisher.Feeds;
using PinkRooster.NugetPublisher.Feeds.Models;
using PinkRooster.NugetPublisher.Validation;

namespace PinkRooster.NugetPublisher.Publishing;

/// <summary>
/// Implements the core mutating publishing engine for pushing NuGet packages and companion symbols to remote feeds.
/// </summary>
public sealed class PackagePublisherService : IPackagePublisherService
{
    private readonly INugetFeedClient _feedClient;
    private readonly PublishSafetyValidator _safetyValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackagePublisherService"/> class with default client and validator.
    /// </summary>
    public PackagePublisherService()
        : this(new NugetFeedClient()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackagePublisherService"/> class with the specified feed client.
    /// </summary>
    /// <param name="feedClient">The feed client used to query and publish packages.</param>
    public PackagePublisherService(INugetFeedClient feedClient)
        : this(feedClient, new PublishSafetyValidator(feedClient)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackagePublisherService"/> class with the specified feed client and safety validator.
    /// </summary>
    /// <param name="feedClient">The feed client used to query and publish packages.</param>
    /// <param name="safetyValidator">The pre-flight safety validator.</param>
    public PackagePublisherService(
        INugetFeedClient feedClient,
        PublishSafetyValidator safetyValidator
    )
    {
        ArgumentNullException.ThrowIfNull(feedClient);
        ArgumentNullException.ThrowIfNull(safetyValidator);
        _feedClient = feedClient;
        _safetyValidator = safetyValidator;
    }

    /// <inheritdoc />
    public async Task<PublishPackageResponse> PublishAsync(
        PublishPackageRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NupkgPath);

        string effectiveFeedUrl = string.IsNullOrWhiteSpace(request.FeedUrl)
            ? CredentialResolver.DefaultFeedUrl
            : request.FeedUrl.Trim();

        // 1. Execute pre-flight check
        ValidationResult validationResult = await _safetyValidator.ValidateAsync(
            request.NupkgPath,
            effectiveFeedUrl,
            ct
        );

        List<string> warnings = validationResult
            .Issues.Where(i => i.Severity == ValidationSeverity.Warning)
            .Select(i => CredentialResolver.Sanitize(i.Message, request.ApiKey) ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        // Check for feed collision
        if (validationResult.HasCollision)
        {
            if (request.SkipDuplicate)
            {
                string skipWarning = CredentialResolver.Sanitize(
                    $"Package '{validationResult.PackageId}' version '{validationResult.Version}' already exists on feed '{effectiveFeedUrl}' and was skipped.",
                    request.ApiKey
                )!;
                warnings.Add(skipWarning);

                return new PublishPackageResponse
                {
                    Success = true,
                    PackageId = validationResult.PackageId,
                    Version = validationResult.Version,
                    FeedUrl = effectiveFeedUrl,
                    PublishedPackage = false,
                    PublishedSymbols = false,
                    StatusCode = 409,
                    Warnings = warnings,
                    Message = skipWarning,
                };
            }

            string collisionMessage = CredentialResolver.Sanitize(
                $"Package '{validationResult.PackageId}' version '{validationResult.Version}' already exists on feed '{effectiveFeedUrl}'. Publishing aborted due to version immutability conflict.",
                request.ApiKey
            )!;

            return new PublishPackageResponse
            {
                Success = false,
                PackageId = validationResult.PackageId,
                Version = validationResult.Version,
                FeedUrl = effectiveFeedUrl,
                PublishedPackage = false,
                PublishedSymbols = false,
                StatusCode = 409,
                Warnings = warnings,
                Message = collisionMessage,
            };
        }

        // Check for other validation errors
        if (!validationResult.IsValid)
        {
            if (!request.Force)
            {
                string errorDetails = string.Join(
                    "; ",
                    validationResult
                        .Issues.Where(i => i.Severity == ValidationSeverity.Error)
                        .Select(i => i.Message)
                );
                string failMessage = CredentialResolver.Sanitize(
                    $"Package pre-flight validation failed: {errorDetails}",
                    request.ApiKey
                )!;

                return new PublishPackageResponse
                {
                    Success = false,
                    PackageId = validationResult.PackageId,
                    Version = validationResult.Version,
                    FeedUrl = effectiveFeedUrl,
                    PublishedPackage = false,
                    PublishedSymbols = false,
                    StatusCode = 400,
                    Warnings = warnings,
                    Message = failMessage,
                };
            }

            foreach (
                ValidationIssue issue in validationResult.Issues.Where(i =>
                    i.Severity == ValidationSeverity.Error
                )
            )
            {
                warnings.Add(
                    CredentialResolver.Sanitize(
                        $"Forced publish despite validation error: {issue.Message}",
                        request.ApiKey
                    )!
                );
            }
        }

        // 2. Resolve credentials
        ResolvedCredential credential = CredentialResolver.Resolve(
            effectiveFeedUrl,
            request.ApiKey
        );
        if (string.IsNullOrEmpty(credential.ApiKey))
        {
            string noCredMessage = CredentialResolver.Sanitize(
                $"No API key or credentials found for feed '{effectiveFeedUrl}' in environment or nuget.config.",
                request.ApiKey
            )!;

            return new PublishPackageResponse
            {
                Success = false,
                PackageId = validationResult.PackageId,
                Version = validationResult.Version,
                FeedUrl = effectiveFeedUrl,
                PublishedPackage = false,
                PublishedSymbols = false,
                StatusCode = 401,
                Warnings = warnings,
                Message = noCredMessage,
            };
        }

        // 3. Upload primary .nupkg
        PushResult pushResult;
        await using (FileStream packageStream = File.OpenRead(request.NupkgPath))
        {
            pushResult = await _feedClient.PushPackageAsync(
                packageStream,
                effectiveFeedUrl,
                credential.ApiKey,
                ct
            );
        }

        if (pushResult.IsConflict)
        {
            if (request.SkipDuplicate)
            {
                string conflictSkipWarning = CredentialResolver.Sanitize(
                    $"Package '{validationResult.PackageId}' version '{validationResult.Version}' already exists on feed '{effectiveFeedUrl}' (HTTP 409 Conflict) and was skipped.",
                    credential.ApiKey
                )!;
                warnings.Add(conflictSkipWarning);

                return new PublishPackageResponse
                {
                    Success = true,
                    PackageId = validationResult.PackageId,
                    Version = validationResult.Version,
                    FeedUrl = effectiveFeedUrl,
                    PublishedPackage = false,
                    PublishedSymbols = false,
                    StatusCode = 409,
                    Warnings = warnings,
                    Message = conflictSkipWarning,
                };
            }

            return new PublishPackageResponse
            {
                Success = false,
                PackageId = validationResult.PackageId,
                Version = validationResult.Version,
                FeedUrl = effectiveFeedUrl,
                PublishedPackage = false,
                PublishedSymbols = false,
                StatusCode = pushResult.StatusCode,
                Warnings = warnings,
                Message =
                    CredentialResolver.Sanitize(pushResult.Message, credential.ApiKey)
                    ?? pushResult.Message,
            };
        }

        if (!pushResult.Success)
        {
            return new PublishPackageResponse
            {
                Success = false,
                PackageId = validationResult.PackageId,
                Version = validationResult.Version,
                FeedUrl = effectiveFeedUrl,
                PublishedPackage = false,
                PublishedSymbols = false,
                StatusCode = pushResult.StatusCode,
                Warnings = warnings,
                Message =
                    CredentialResolver.Sanitize(pushResult.Message, credential.ApiKey)
                    ?? pushResult.Message,
            };
        }

        // 4. Companion Symbol Sequencing (Review Focus #2): Primary package MUST be pushed first
        string? snupkgPath =
            validationResult.ArchiveInfo?.CompanionSymbolsPath
            ?? Path.ChangeExtension(request.NupkgPath, ".snupkg");
        bool hasCompanionSymbols =
            validationResult.ArchiveInfo?.HasCompanionSymbols
            ?? (snupkgPath is not null && File.Exists(snupkgPath));

        bool publishedSymbols = false;
        if (hasCompanionSymbols && !string.IsNullOrEmpty(snupkgPath) && File.Exists(snupkgPath))
        {
            string? symbolTarget = request.SymbolFeedUrl ?? effectiveFeedUrl;
            PushResult symbolPushResult;
            await using (FileStream symbolStream = File.OpenRead(snupkgPath))
            {
                symbolPushResult = await _feedClient.PushSymbolPackageAsync(
                    symbolStream,
                    symbolTarget,
                    credential.ApiKey,
                    ct
                );
            }

            if (symbolPushResult.Success)
            {
                publishedSymbols = true;
            }
            else
            {
                string symbolFailWarning = CredentialResolver.Sanitize(
                    $"Companion symbol package push failed: {symbolPushResult.Message}",
                    credential.ApiKey
                )!;
                warnings.Add(symbolFailWarning);
            }
        }
        else
        {
            warnings.Add($"No companion symbol package (.snupkg) found for '{request.NupkgPath}'.");
        }

        string finalMessage = CredentialResolver.Sanitize(
            publishedSymbols
                ? $"Package '{validationResult.PackageId}' version '{validationResult.Version}' and symbols successfully published to '{effectiveFeedUrl}'."
                : $"Package '{validationResult.PackageId}' version '{validationResult.Version}' successfully published to '{effectiveFeedUrl}'.",
            credential.ApiKey
        )!;

        return new PublishPackageResponse
        {
            Success = true,
            PackageId = validationResult.PackageId,
            Version = validationResult.Version,
            FeedUrl = effectiveFeedUrl,
            PublishedPackage = true,
            PublishedSymbols = publishedSymbols,
            SymbolPackagePath = hasCompanionSymbols ? snupkgPath : null,
            StatusCode = pushResult.StatusCode,
            Warnings = warnings,
            Message = finalMessage,
        };
    }
}
