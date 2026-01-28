using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models.Content;

/// <summary>
/// Represents text content in an MCP response.
/// </summary>
public sealed class McpTextContent : McpContent
{
    /// <summary>
    /// Gets the content type ("text").
    /// </summary>
    [JsonPropertyName("type")]
    public override string Type => "text";

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new text content instance.
    /// </summary>
    public McpTextContent() { }

    /// <summary>
    /// Creates a new text content instance with the specified text.
    /// </summary>
    /// <param name="text">The text content.</param>
    public McpTextContent(string text)
    {
        Text = text;
    }
}
