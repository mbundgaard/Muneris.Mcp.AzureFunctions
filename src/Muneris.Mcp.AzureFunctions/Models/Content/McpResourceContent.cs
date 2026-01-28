using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models.Content;

/// <summary>
/// Represents a resource reference or embedded resource content in an MCP response.
/// </summary>
public sealed class McpResourceContent : McpContent
{
    /// <summary>
    /// Gets the content type ("resource").
    /// </summary>
    [JsonPropertyName("type")]
    public override string Type => "resource";

    /// <summary>
    /// Gets or sets the URI of the resource.
    /// </summary>
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type of the resource content.
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/json";

    /// <summary>
    /// Gets or sets the text content of the resource (for text-based resources).
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded blob content (for binary resources).
    /// </summary>
    [JsonPropertyName("blob")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Blob { get; set; }

    /// <summary>
    /// Creates a new resource content instance.
    /// </summary>
    public McpResourceContent() { }

    /// <summary>
    /// Creates a new resource content instance with text content.
    /// </summary>
    /// <param name="uri">The resource URI.</param>
    /// <param name="mimeType">The MIME type.</param>
    /// <param name="text">The text content.</param>
    public static McpResourceContent FromText(string uri, string mimeType, string text)
    {
        return new McpResourceContent
        {
            Uri = uri,
            MimeType = mimeType,
            Text = text
        };
    }

    /// <summary>
    /// Creates a new resource content instance with binary content.
    /// </summary>
    /// <param name="uri">The resource URI.</param>
    /// <param name="mimeType">The MIME type.</param>
    /// <param name="blob">Base64-encoded binary content.</param>
    public static McpResourceContent FromBlob(string uri, string mimeType, string blob)
    {
        return new McpResourceContent
        {
            Uri = uri,
            MimeType = mimeType,
            Blob = blob
        };
    }
}
