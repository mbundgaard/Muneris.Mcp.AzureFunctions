using System.Text.Json;
using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models;

/// <summary>
/// Represents a JSON-RPC 2.0 request message.
/// </summary>
public sealed class JsonRpcRequest
{
    /// <summary>
    /// The JSON-RPC protocol version. Must be "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// The request identifier. Can be a string or number.
    /// Notifications (which don't expect a response) may omit this.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    /// <summary>
    /// The method to invoke.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// The method parameters. Can be an object or array.
    /// </summary>
    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}
