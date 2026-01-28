using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models;

/// <summary>
/// Annotations providing hints to LLMs about tool behavior.
/// These are optional hints that help LLMs decide when and how to use tools.
/// </summary>
public sealed class McpToolAnnotations
{
    /// <summary>
    /// Gets or sets the human-readable title for the tool.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets whether this tool only reads data without modifying state.
    /// When true, hints that the tool is safe to call without side effects.
    /// </summary>
    [JsonPropertyName("readOnlyHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReadOnlyHint { get; set; }

    /// <summary>
    /// Gets or sets whether this tool performs destructive operations.
    /// When true, hints to use caution before invoking.
    /// </summary>
    [JsonPropertyName("destructiveHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DestructiveHint { get; set; }

    /// <summary>
    /// Gets or sets whether this tool is idempotent.
    /// When true, hints that repeated calls are safe.
    /// </summary>
    [JsonPropertyName("idempotentHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IdempotentHint { get; set; }

    /// <summary>
    /// Gets or sets whether this tool interacts with external systems.
    /// When true, indicates the tool may access external services.
    /// </summary>
    [JsonPropertyName("openWorldHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OpenWorldHint { get; set; }

    /// <summary>
    /// Returns true if any annotation is set (non-default value).
    /// </summary>
    [JsonIgnore]
    public bool HasAnnotations =>
        Title is not null ||
        ReadOnlyHint is not null ||
        DestructiveHint is not null ||
        IdempotentHint is not null ||
        OpenWorldHint is not null;
}
