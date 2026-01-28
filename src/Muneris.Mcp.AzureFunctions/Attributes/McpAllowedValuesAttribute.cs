namespace Muneris.Mcp.AzureFunctions.Attributes;

/// <summary>
/// Specifies allowed values for a property, generating a JSON Schema enum.
/// This attribute provides a fallback for environments without .NET 8's AllowedValuesAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class McpAllowedValuesAttribute : Attribute
{
    /// <summary>
    /// Gets the allowed values.
    /// </summary>
    public object[] Values { get; }

    /// <summary>
    /// Creates a new allowed values attribute.
    /// </summary>
    /// <param name="values">The allowed values for this property.</param>
    public McpAllowedValuesAttribute(params object[] values)
    {
        Values = values ?? Array.Empty<object>();
    }
}
