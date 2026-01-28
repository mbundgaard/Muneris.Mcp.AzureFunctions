using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models.Resources;

/// <summary>
/// Represents resource metadata returned in resources/list responses.
/// </summary>
public sealed class McpResourceInfo
{
    /// <summary>
    /// Gets or sets the URI of the resource.
    /// </summary>
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable name of the resource.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the resource.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the resource content.
    /// </summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; set; }
}
