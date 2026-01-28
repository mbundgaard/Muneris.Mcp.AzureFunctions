namespace Muneris.Mcp.AzureFunctions.Attributes;

/// <summary>
/// Defines a parameter for an MCP tool.
/// Apply to the tool method or parameter to describe its input parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true, Inherited = false)]
public sealed class McpToolPropertyAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the property/parameter.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the JSON Schema type of the property.
    /// Common values: "string", "number", "integer", "boolean", "array", "object".
    /// Default is "string".
    /// </summary>
    public string Type { get; set; } = "string";

    /// <summary>
    /// Gets or sets the description of the property.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this property is required.
    /// Default is false.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the default value for this property.
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// Gets or sets the allowed values for this property (creates JSON Schema enum).
    /// </summary>
    public string[]? Enum { get; set; }

    /// <summary>
    /// Gets or sets the JSON Schema format for string types.
    /// Common values: "date", "date-time", "email", "uri", "uuid", "hostname".
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the minimum value for numeric types.
    /// </summary>
    public double Minimum { get; set; } = double.NaN;

    /// <summary>
    /// Gets or sets the maximum value for numeric types.
    /// </summary>
    public double Maximum { get; set; } = double.NaN;

    /// <summary>
    /// Gets or sets the minimum length for string types.
    /// </summary>
    public int MinLength { get; set; } = -1;

    /// <summary>
    /// Gets or sets the maximum length for string types.
    /// </summary>
    public int MaxLength { get; set; } = -1;

    /// <summary>
    /// Gets or sets the regex pattern for string validation.
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Creates a new MCP tool property attribute.
    /// </summary>
    /// <param name="name">The name of the property/parameter.</param>
    public McpToolPropertyAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
