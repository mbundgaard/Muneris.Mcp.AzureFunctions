using System.Text.Json.Serialization;
using Muneris.Mcp.AzureFunctions.Models.Content;

namespace Muneris.Mcp.AzureFunctions.Models.Resources;

/// <summary>
/// Represents the result of a resource read operation.
/// </summary>
public sealed class McpResourceResult
{
    /// <summary>
    /// Gets or sets the resource contents.
    /// </summary>
    [JsonPropertyName("contents")]
    public McpResourceContentItem[] Contents { get; set; } = Array.Empty<McpResourceContentItem>();
}

/// <summary>
/// Represents a single content item in a resource read result.
/// </summary>
public sealed class McpResourceContentItem
{
    /// <summary>
    /// Gets or sets the URI of the resource.
    /// </summary>
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type of the content.
    /// </summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets the text content (for text-based resources).
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
    /// Creates a text-based resource content item.
    /// </summary>
    public static McpResourceContentItem FromText(string uri, string mimeType, string text)
    {
        return new McpResourceContentItem
        {
            Uri = uri,
            MimeType = mimeType,
            Text = text
        };
    }

    /// <summary>
    /// Creates a binary resource content item.
    /// </summary>
    public static McpResourceContentItem FromBlob(string uri, string mimeType, string blob)
    {
        return new McpResourceContentItem
        {
            Uri = uri,
            MimeType = mimeType,
            Blob = blob
        };
    }
}
