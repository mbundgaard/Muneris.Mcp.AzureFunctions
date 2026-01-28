using System.Text.Json;
using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models;

/// <summary>
/// Represents a JSON-RPC 2.0 response message.
/// </summary>
public sealed class JsonRpcResponse
{
    /// <summary>
    /// The JSON-RPC protocol version. Always "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// The request identifier that this response corresponds to.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    /// <summary>
    /// The result of the method invocation. Present on success.
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    /// <summary>
    /// The error object. Present on failure.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }

    /// <summary>
    /// Creates a success response.
    /// </summary>
    public static JsonRpcResponse Success(JsonElement? id, object? result)
    {
        return new JsonRpcResponse { Id = id, Result = result };
    }

    /// <summary>
    /// Creates an error response.
    /// </summary>
    public static JsonRpcResponse Failure(JsonElement? id, int code, string message, object? data = null)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message, Data = data }
        };
    }
}
