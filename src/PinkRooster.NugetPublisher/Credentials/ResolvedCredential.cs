namespace PinkRooster.NugetPublisher.Credentials;

/// <summary>
/// Defines well-known source identifiers for resolved NuGet credentials.
/// </summary>
public static class CredentialSources
{
    /// <summary>
    /// The credential was supplied explicitly via parameters.
    /// </summary>
    public const string Explicit = "Explicit";

    /// <summary>
    /// The credential was resolved from ambient environment variables.
    /// </summary>
    public const string Environment = "Environment";

    /// <summary>
    /// The credential was resolved from hierarchical nuget.config settings.
    /// </summary>
    public const string NuGetConfig = "NuGetConfig";

    /// <summary>
    /// No credential was resolved for the feed.
    /// </summary>
    public const string None = "None";
}

/// <summary>
/// Represents resolved authentication credentials for interacting with a NuGet feed.
/// </summary>
public sealed class ResolvedCredential
{
    /// <summary>
    /// Gets the resolved API key or secret token, if present.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the source from which the credential was resolved (e.g. "Explicit", "Environment", "NuGetConfig", or "None").
    /// </summary>
    public string Source { get; init; } = CredentialSources.None;

    /// <summary>
    /// Gets the feed URL for which credentials were resolved.
    /// </summary>
    public string? FeedUrl { get; init; }

    /// <summary>
    /// Gets the HTTP header name for transmitting the credential (e.g. "X-NuGet-ApiKey" or "Authorization").
    /// </summary>
    public string? HeaderName { get; init; }

    /// <summary>
    /// Gets the HTTP header value for transmitting the credential.
    /// </summary>
    public string? HeaderValue { get; init; }

    /// <summary>
    /// Gets a value indicating whether valid credentials were resolved.
    /// </summary>
    public bool HasCredentials =>
        !string.IsNullOrEmpty(ApiKey) || !string.IsNullOrEmpty(HeaderValue);
}
