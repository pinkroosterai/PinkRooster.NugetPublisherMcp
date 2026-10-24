namespace PinkRooster.NugetPublisher.Feeds.Models;

/// <summary>
/// Represents the result of pushing a package or symbol package to a NuGet feed.
/// </summary>
public sealed class PushResult
{
    /// <summary>
    /// Gets a value indicating whether the push operation succeeded (HTTP 2xx).
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the HTTP status code returned by the feed.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the response message or diagnostic detail, sanitized of any secrets.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target endpoint URL to which the payload was pushed.
    /// </summary>
    public string? EndpointUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether the rejection was due to a version conflict (HTTP 409 Conflict).
    /// </summary>
    public bool IsConflict => StatusCode == 409;
}
