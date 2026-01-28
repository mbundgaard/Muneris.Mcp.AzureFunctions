using System.Text.Json.Serialization;
using Muneris.Mcp.AzureFunctions.Models.Content;

namespace Muneris.Mcp.AzureFunctions.Models;

/// <summary>
/// Represents the result of a tool invocation.
/// </summary>
public sealed class McpToolResult
{
    /// <summary>
    /// Gets or sets the content items returned by the tool.
    /// </summary>
    [JsonPropertyName("content")]
    public McpContent[] Content { get; set; } = Array.Empty<McpContent>();

    /// <summary>
    /// Gets or sets whether the result represents an error.
    /// Default is false.
    /// </summary>
    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsError { get; set; }

    /// <summary>
    /// Creates a successful tool result with text content.
    /// </summary>
    public static McpToolResult Text(string text)
    {
        return new McpToolResult
        {
            Content = new McpContent[] { new McpTextContent(text) }
        };
    }

    /// <summary>
    /// Creates a successful tool result with multiple content items.
    /// </summary>
    public static McpToolResult WithContent(params McpContent[] content)
    {
        return new McpToolResult { Content = content };
    }

    /// <summary>
    /// Creates an error result with the specified message.
    /// </summary>
    public static McpToolResult Error(string message)
    {
        return new McpToolResult
        {
            Content = new McpContent[] { new McpTextContent(message) },
            IsError = true
        };
    }
}
