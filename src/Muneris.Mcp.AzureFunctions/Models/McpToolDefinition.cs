using System.Text.Json.Serialization;

namespace Muneris.Mcp.AzureFunctions.Models;

/// <summary>
/// Represents an MCP tool definition for the tools/list response.
/// </summary>
public sealed class McpToolDefinition
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A description of what the tool does.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// JSON Schema describing the tool's input parameters.
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public McpToolInputSchema InputSchema { get; set; } = new();

    /// <summary>
    /// Optional annotations providing hints to LLMs about tool behavior.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpToolAnnotations? Annotations { get; set; }
}

/// <summary>
/// JSON Schema for tool input parameters.
/// </summary>
public sealed class McpToolInputSchema
{
    /// <summary>
    /// The schema type. Always "object" for tool inputs.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    /// <summary>
    /// The properties of the input object.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, McpToolPropertySchema> Properties { get; set; } = new();

    /// <summary>
    /// List of required property names.
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }
}

/// <summary>
/// JSON Schema for a single tool property.
/// </summary>
public sealed class McpToolPropertySchema
{
    /// <summary>
    /// The JSON Schema type (string, number, integer, boolean, array, object).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    /// <summary>
    /// Description of the property.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// The default value for this property.
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Default { get; set; }

    /// <summary>
    /// Allowed values (enum constraint).
    /// </summary>
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Enum { get; set; }

    /// <summary>
    /// Format hint for string types (date, email, uri, uuid, etc.).
    /// </summary>
    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    /// <summary>
    /// Minimum value for numeric types.
    /// </summary>
    [JsonPropertyName("minimum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Minimum { get; set; }

    /// <summary>
    /// Maximum value for numeric types.
    /// </summary>
    [JsonPropertyName("maximum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Maximum { get; set; }

    /// <summary>
    /// Minimum length for string types.
    /// </summary>
    [JsonPropertyName("minLength")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinLength { get; set; }

    /// <summary>
    /// Maximum length for string types.
    /// </summary>
    [JsonPropertyName("maxLength")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxLength { get; set; }

    /// <summary>
    /// Regex pattern for string validation.
    /// </summary>
    [JsonPropertyName("pattern")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Pattern { get; set; }

    /// <summary>
    /// Schema for array items (when Type is "array").
    /// </summary>
    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpToolPropertySchema? Items { get; set; }

    /// <summary>
    /// Properties for nested objects (when Type is "object").
    /// </summary>
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, McpToolPropertySchema>? Properties { get; set; }

    /// <summary>
    /// Required properties for nested objects.
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }
}
