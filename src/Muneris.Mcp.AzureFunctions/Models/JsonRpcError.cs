using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models;

/// <summary>
/// Represents a JSON-RPC 2.0 error object.
/// </summary>
public sealed class JsonRpcError
{
    /// <summary>
    /// The error code.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// A short description of the error.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional data about the error.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

/// <summary>
/// Standard JSON-RPC 2.0 error codes.
/// </summary>
public static class JsonRpcErrorCodes
{
    /// <summary>Invalid JSON was received by the server.</summary>
    public const int ParseError = -32700;

    /// <summary>The JSON sent is not a valid Request object.</summary>
    public const int InvalidRequest = -32600;

    /// <summary>The method does not exist / is not available.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>Invalid method parameter(s).</summary>
    public const int InvalidParams = -32602;

    /// <summary>Internal JSON-RPC error.</summary>
    public const int InternalError = -32603;

    /// <summary>Authentication required or failed (custom MCP error).</summary>
    public const int AuthenticationError = -32001;

    /// <summary>Resource not found (custom MCP error).</summary>
    public const int ResourceNotFound = -32002;
}
