using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using NuGet.Configuration;

namespace PinkRooster.NugetPublisher.Credentials;

/// <summary>
/// Resolves authentication credentials for NuGet feeds and sanitizes diagnostic messages to prevent secret leaks.
/// </summary>
public static partial class CredentialResolver
{
    /// <summary>
    /// The default NuGet v3 service index URL for nuget.org.
    /// </summary>
    public const string DefaultFeedUrl = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// The redaction placeholder substituted for sensitive tokens and keys in sanitized text.
    /// </summary>
    public const string RedactedPlaceholder = "***REDACTED***";

    private static readonly ConcurrentDictionary<string, byte> ActiveResolvedKeys = new(
        StringComparer.Ordinal
    );

    [GeneratedRegex(@"\boy2[a-zA-Z0-9_-]{20,}\b")]
    private static partial Regex NugetApiKeyRegex();

    [GeneratedRegex(@"\bghp_[a-zA-Z0-9]{20,}\b")]
    private static partial Regex GitHubClassicPatRegex();

    [GeneratedRegex(@"\bgithub_pat_[a-zA-Z0-9_]{20,}\b")]
    private static partial Regex GitHubFineGrainedPatRegex();

    [GeneratedRegex(@"\bgh[ousr]_[a-zA-Z0-9]{20,}\b")]
    private static partial Regex GitHubOtherTokenRegex();

    [GeneratedRegex(@"(?i)\b(bearer\s+)[a-zA-Z0-9_\-\.\~+/=]+")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?i)\b(basic\s+)[a-zA-Z0-9+/=]{8,}")]
    private static partial Regex BasicAuthRegex();

    [GeneratedRegex(@"https?://([^:@\s\r\n]+):([^@\s\r\n]+)@")]
    private static partial Regex UrlEmbeddedCredentialRegex();

    [GeneratedRegex(@"(?i)\b(x-nuget-apikey\s*[:=]\s*)[^\s\r\n]+")]
    private static partial Regex XNugetApiKeyHeaderRegex();

    [GeneratedRegex(@"\b[a-zA-Z0-9]{52}\b")]
    private static partial Regex AzureDevOpsPatRegex();

    /// <summary>
    /// Registers a sensitive key or token to be redacted by subsequent calls to <see cref="Sanitize(string?, string?)"/>.
    /// </summary>
    /// <param name="key">The sensitive key or token to register.</param>
    public static void RegisterActiveKey(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key) && key.Length >= 4)
        {
            ActiveResolvedKeys.TryAdd(key, 0);
        }
    }

    /// <summary>
    /// Clears all dynamically registered active keys from the sanitizer.
    /// </summary>
    public static void ClearActiveKeys()
    {
        ActiveResolvedKeys.Clear();
    }

    /// <summary>
    /// Resolves authentication credentials for a target NuGet feed.
    /// </summary>
    /// <param name="feedUrl">The target NuGet feed URL, defaulting to <see cref="DefaultFeedUrl"/> if omitted or empty.</param>
    /// <param name="explicitKey">An explicit API key or token provided directly by the caller.</param>
    /// <returns>A <see cref="ResolvedCredential"/> containing authentication parameters.</returns>
    public static ResolvedCredential Resolve(string? feedUrl = null, string? explicitKey = null) =>
        Resolve(feedUrl, explicitKey, getEnvironmentVariable: null, settings: null);

    /// <summary>
    /// Resolves authentication credentials for a target NuGet feed using optional custom environment and settings providers.
    /// </summary>
    /// <param name="feedUrl">The target NuGet feed URL, defaulting to <see cref="DefaultFeedUrl"/> if omitted or empty.</param>
    /// <param name="explicitKey">An explicit API key or token provided directly by the caller.</param>
    /// <param name="getEnvironmentVariable">A function used to resolve environment variables, defaulting to <see cref="Environment.GetEnvironmentVariable(string)"/>.</param>
    /// <param name="settings">Hierarchical NuGet configuration settings, defaulting to default settings if null.</param>
    /// <returns>A <see cref="ResolvedCredential"/> containing authentication parameters.</returns>
    public static ResolvedCredential Resolve(
        string? feedUrl,
        string? explicitKey,
        Func<string, string?>? getEnvironmentVariable,
        ISettings? settings = null
    )
    {
        string effectiveFeedUrl = string.IsNullOrWhiteSpace(feedUrl)
            ? DefaultFeedUrl
            : feedUrl.Trim();

        // 1. Explicit key
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            string key = explicitKey.Trim();
            RegisterActiveKey(key);

            bool isAuthorizationHeader =
                key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);

            return new ResolvedCredential
            {
                ApiKey = key,
                Source = CredentialSources.Explicit,
                FeedUrl = effectiveFeedUrl,
                HeaderName = isAuthorizationHeader ? "Authorization" : "X-NuGet-ApiKey",
                HeaderValue = key,
            };
        }

        // 2. Ambient environment variables
        Func<string, string?> envLookup =
            getEnvironmentVariable ?? Environment.GetEnvironmentVariable;

        bool isGitHub =
            effectiveFeedUrl.Contains("nuget.pkg.github.com", StringComparison.OrdinalIgnoreCase)
            || effectiveFeedUrl.Contains("github", StringComparison.OrdinalIgnoreCase);

        bool isAzure =
            effectiveFeedUrl.Contains("pkgs.visualstudio.com", StringComparison.OrdinalIgnoreCase)
            || effectiveFeedUrl.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase);

        string[] envVarCandidates =
            isGitHub ? ["GH_TOKEN", "GITHUB_TOKEN", "NUGET_API_KEY", "NUGET_AUTH_TOKEN"]
            : isAzure
                ?
                [
                    "AZURE_DEVOPS_EXT_PAT",
                    "SYSTEM_ACCESSTOKEN",
                    "NUGET_API_KEY",
                    "NUGET_AUTH_TOKEN",
                ]
            : ["NUGET_API_KEY", "NUGET_AUTH_TOKEN"];

        foreach (string varName in envVarCandidates)
        {
            string? value = envLookup(varName)?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            RegisterActiveKey(value);

            string headerName;
            string headerValue;

            if (string.Equals(varName, "SYSTEM_ACCESSTOKEN", StringComparison.OrdinalIgnoreCase))
            {
                headerName = "Authorization";
                headerValue = $"Bearer {value}";
            }
            else if (
                string.Equals(varName, "AZURE_DEVOPS_EXT_PAT", StringComparison.OrdinalIgnoreCase)
            )
            {
                headerName = "Authorization";
                headerValue =
                    "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes($":{value}"));
            }
            else if (
                string.Equals(varName, "NUGET_AUTH_TOKEN", StringComparison.OrdinalIgnoreCase)
                && (
                    value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                headerName = "Authorization";
                headerValue = value;
            }
            else
            {
                headerName = "X-NuGet-ApiKey";
                headerValue = value;
            }

            return new ResolvedCredential
            {
                ApiKey = value,
                Source = CredentialSources.Environment,
                FeedUrl = effectiveFeedUrl,
                HeaderName = headerName,
                HeaderValue = headerValue,
            };
        }

        // 3. Hierarchical nuget.config
        try
        {
            ISettings nugetSettings = settings ?? Settings.LoadDefaultSettings(null);
            PackageSourceProvider provider = new(nugetSettings);
            IEnumerable<PackageSource> sources = provider.LoadPackageSources();
            PackageSource? match = FindMatchingSource(sources, effectiveFeedUrl);

            if (match?.Credentials is { } cred)
            {
                string? username = cred.Username;
                string? password = cred.Password;
                if (string.IsNullOrEmpty(password))
                {
                    password = cred.PasswordText;
                }

                if (!string.IsNullOrEmpty(password))
                {
                    RegisterActiveKey(password);

                    string headerName = !string.IsNullOrEmpty(username)
                        ? "Authorization"
                        : "X-NuGet-ApiKey";
                    string headerValue = !string.IsNullOrEmpty(username)
                        ? "Basic "
                            + Convert.ToBase64String(
                                Encoding.UTF8.GetBytes($"{username}:{password}")
                            )
                        : password;

                    return new ResolvedCredential
                    {
                        ApiKey = password,
                        Source = CredentialSources.NuGetConfig,
                        FeedUrl = effectiveFeedUrl,
                        HeaderName = headerName,
                        HeaderValue = headerValue,
                    };
                }
            }

            if (match is not null)
            {
                string? apiKeyFromSettings =
                    SettingsUtility.GetDecryptedValueForAddItem(
                        nugetSettings,
                        "apikeys",
                        match.Source,
                        false
                    )
                    ?? SettingsUtility.GetDecryptedValueForAddItem(
                        nugetSettings,
                        "apikeys",
                        effectiveFeedUrl,
                        false
                    );

                if (!string.IsNullOrEmpty(apiKeyFromSettings))
                {
                    RegisterActiveKey(apiKeyFromSettings);

                    return new ResolvedCredential
                    {
                        ApiKey = apiKeyFromSettings,
                        Source = CredentialSources.NuGetConfig,
                        FeedUrl = effectiveFeedUrl,
                        HeaderName = "X-NuGet-ApiKey",
                        HeaderValue = apiKeyFromSettings,
                    };
                }
            }
        }
        catch
        {
            // nuget.config could not be read; fallback to unauthenticated
        }

        // 4. No credentials found
        return new ResolvedCredential
        {
            ApiKey = null,
            Source = CredentialSources.None,
            FeedUrl = effectiveFeedUrl,
            HeaderName = null,
            HeaderValue = null,
        };
    }

    /// <summary>
    /// Sanitizes diagnostic messages or exceptions by redacting known credential patterns and active keys.
    /// </summary>
    /// <param name="message">The text to sanitize.</param>
    /// <param name="activeKey">An optional active key or secret to explicitly redact.</param>
    /// <returns>The sanitized string with all secrets replaced by <see cref="RedactedPlaceholder"/>.</returns>
    public static string Sanitize(string? message, string? activeKey = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        string result = message;

        // 1. Explicit active key passed to Sanitize
        if (!string.IsNullOrWhiteSpace(activeKey) && activeKey.Length >= 4)
        {
            result = result.Replace(activeKey, RedactedPlaceholder);
        }

        // 2. Globally tracked active resolved keys
        foreach (string key in ActiveResolvedKeys.Keys)
        {
            if (key.Length >= 4)
            {
                result = result.Replace(key, RedactedPlaceholder);
            }
        }

        // 3. Known credential patterns
        result = NugetApiKeyRegex().Replace(result, RedactedPlaceholder);
        result = GitHubClassicPatRegex().Replace(result, RedactedPlaceholder);
        result = GitHubFineGrainedPatRegex().Replace(result, RedactedPlaceholder);
        result = GitHubOtherTokenRegex().Replace(result, RedactedPlaceholder);
        result = AzureDevOpsPatRegex().Replace(result, RedactedPlaceholder);
        result = BearerTokenRegex().Replace(result, $"$1{RedactedPlaceholder}");
        result = BasicAuthRegex().Replace(result, $"$1{RedactedPlaceholder}");
        result = UrlEmbeddedCredentialRegex().Replace(result, $"https://$1:{RedactedPlaceholder}@");
        result = XNugetApiKeyHeaderRegex().Replace(result, $"$1{RedactedPlaceholder}");

        return result;
    }

    private static PackageSource? FindMatchingSource(
        IEnumerable<PackageSource> sources,
        string targetUrl
    )
    {
        string target = targetUrl.Trim().TrimEnd('/');
        List<PackageSource> list = [.. sources];

        PackageSource? match = list.FirstOrDefault(s =>
            string.Equals(s.Source?.Trim().TrimEnd('/'), target, StringComparison.OrdinalIgnoreCase)
        );

        if (match is not null)
        {
            return match;
        }

        if (Uri.TryCreate(targetUrl, UriKind.Absolute, out Uri? targetUri))
        {
            match = list.FirstOrDefault(s =>
                Uri.TryCreate(s.Source, UriKind.Absolute, out Uri? sUri)
                && string.Equals(sUri.Host, targetUri.Host, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    sUri.AbsolutePath.TrimEnd('/'),
                    targetUri.AbsolutePath.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (match is not null)
            {
                return match;
            }
        }

        match = list.FirstOrDefault(s =>
            string.Equals(s.Name?.Trim(), target, StringComparison.OrdinalIgnoreCase)
        );

        if (match is not null)
        {
            return match;
        }

        return list.FirstOrDefault(s =>
            !string.IsNullOrEmpty(s.Source)
            && (
                target.Contains(s.Source.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                || s.Source.Trim().TrimEnd('/').Contains(target, StringComparison.OrdinalIgnoreCase)
            )
        );
    }
}
