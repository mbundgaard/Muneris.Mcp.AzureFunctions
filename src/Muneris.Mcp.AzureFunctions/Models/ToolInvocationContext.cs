using System.Security.Claims;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace Muneris.Mcp.AzureFunctions.Models;

/// <summary>
/// Context passed to MCP tool handlers during invocation.
/// Provides access to arguments, authentication info, and the underlying HTTP request.
/// </summary>
public sealed class ToolInvocationContext
{
    /// <summary>
    /// The name of the tool being invoked.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// The arguments passed to the tool, deserialized from JSON.
    /// </summary>
    public JsonElement? Arguments { get; }

    /// <summary>
    /// The authenticated user, if any. Null for anonymous requests.
    /// </summary>
    public ClaimsPrincipal? User { get; }

    /// <summary>
    /// The MCP session ID, if provided by the client.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// The MCP protocol version from the client.
    /// </summary>
    public string? ProtocolVersion { get; }

    /// <summary>
    /// The underlying HTTP request for accessing headers and other request data.
    /// </summary>
    public HttpRequestData Request { get; }

    /// <summary>
    /// The cancellation token for the request.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new tool invocation context.
    /// </summary>
    public ToolInvocationContext(
        string toolName,
        JsonElement? arguments,
        ClaimsPrincipal? user,
        string? sessionId,
        HttpRequestData request,
        string? protocolVersion = null,
        CancellationToken cancellationToken = default)
    {
        ToolName = toolName;
        Arguments = arguments;
        User = user;
        SessionId = sessionId;
        ProtocolVersion = protocolVersion;
        Request = request;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets a string argument by name.
    /// </summary>
    public string? GetString(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (Arguments.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    /// <summary>
    /// Gets an integer argument by name.
    /// </summary>
    public int? GetInt32(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (Arguments.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number)
            return value.GetInt32();

        return null;
    }

    /// <summary>
    /// Gets a boolean argument by name.
    /// </summary>
    public bool? GetBoolean(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (Arguments.Value.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
        }

        return null;
    }

    /// <summary>
    /// Gets an argument deserialized to a specific type.
    /// </summary>
    public T? GetValue<T>(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return default;

        if (Arguments.Value.TryGetProperty(name, out var value))
            return JsonSerializer.Deserialize<T>(value.GetRawText());

        return default;
    }

    /// <summary>
    /// Checks if an argument exists.
    /// </summary>
    public bool HasArgument(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return false;

        return Arguments.Value.TryGetProperty(name, out _);
    }
}
