using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models.Content;

/// <summary>
/// Represents image content in an MCP response.
/// </summary>
public sealed class McpImageContent : McpContent
{
    /// <summary>
    /// Gets the content type ("image").
    /// </summary>
    [JsonPropertyName("type")]
    public override string Type => "image";

    /// <summary>
    /// Gets or sets the base64-encoded image data.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type of the image (e.g., "image/png", "image/jpeg").
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "image/png";

    /// <summary>
    /// Creates a new image content instance.
    /// </summary>
    public McpImageContent() { }

    /// <summary>
    /// Creates a new image content instance with the specified data and MIME type.
    /// </summary>
    /// <param name="data">Base64-encoded image data.</param>
    /// <param name="mimeType">The MIME type of the image.</param>
    public McpImageContent(string data, string mimeType)
    {
        Data = data;
        MimeType = mimeType;
    }
}
