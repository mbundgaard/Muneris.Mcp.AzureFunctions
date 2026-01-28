using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models.Content;

/// <summary>
/// Base class for MCP content types.
/// Content can be text, image, or resource reference.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(McpTextContent), "text")]
[JsonDerivedType(typeof(McpImageContent), "image")]
[JsonDerivedType(typeof(McpResourceContent), "resource")]
public abstract class McpContent
{
    /// <summary>
    /// Gets the content type discriminator.
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}
