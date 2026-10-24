using System.Text.Json;
using ModelContextProtocol.Protocol;
using PinkRooster.NugetPublisher.Credentials;

namespace PinkRooster.NugetPublisherMcp.Tools;

/// <summary>
/// Provides utility methods for creating serialized and sanitized <see cref="CallToolResult"/> instances.
/// </summary>
internal static class ToolResultHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Creates a successful <see cref="CallToolResult"/> by serializing the specified payload to JSON.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="payload">The payload to serialize.</param>
    /// <returns>A successful <see cref="CallToolResult"/> containing the serialized JSON content.</returns>
    public static CallToolResult CreateSuccessResult<T>(T payload)
    {
        string json = JsonSerializer.Serialize(payload, SerializerOptions);
        return new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = json }],
        };
    }

    /// <summary>
    /// Creates a failed <see cref="CallToolResult"/> containing a sanitized error message.
    /// </summary>
    /// <param name="message">The raw error message.</param>
    /// <param name="additionalSecret">An optional additional secret to redact.</param>
    /// <returns>A failed <see cref="CallToolResult"/> containing the sanitized message.</returns>
    public static CallToolResult CreateErrorResult(string message, string? additionalSecret = null)
    {
        string sanitized = CredentialResolver.Sanitize(message, additionalSecret);
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = sanitized }],
        };
    }

    /// <summary>
    /// Creates a failed <see cref="CallToolResult"/> from an exception with its message sanitized of secrets.
    /// </summary>
    /// <param name="ex">The thrown exception.</param>
    /// <param name="additionalSecret">An optional additional secret to redact.</param>
    /// <returns>A failed <see cref="CallToolResult"/>.</returns>
    public static CallToolResult CreateErrorResult(Exception ex, string? additionalSecret = null) =>
        CreateErrorResult(ex.Message, additionalSecret);

    /// <summary>
    /// Creates a <see cref="CallToolResult"/> reflecting an operation outcome, serializing the payload and sanitizing any secrets.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="payload">The payload object to serialize.</param>
    /// <param name="additionalSecret">An optional additional secret to redact.</param>
    /// <returns>A <see cref="CallToolResult"/> with appropriate success/error flag and sanitized payload text.</returns>
    public static CallToolResult CreateResult<T>(
        bool success,
        T payload,
        string? additionalSecret = null
    )
    {
        string json = JsonSerializer.Serialize(payload, SerializerOptions);
        string sanitized = CredentialResolver.Sanitize(json, additionalSecret);
        return new CallToolResult
        {
            IsError = !success,
            Content = [new TextContentBlock { Text = sanitized }],
        };
    }
}
