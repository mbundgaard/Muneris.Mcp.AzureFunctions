namespace Muneris.Mcp.AzureFunctions.Attributes;

/// <summary>
/// Specifies allowed values for a property, generating a JSON Schema enum.
/// This attribute provides a fallback for environments without .NET 8's AllowedValuesAttribute.
/// </summary>
/// <example>
/// Use this attribute on POCO properties to constrain values to a specific set:
/// <code>
/// public class CreateOrderRequest
/// {
///     [Description("Order priority level")]
///     [McpAllowedValues("low", "normal", "high", "urgent")]
///     public string Priority { get; set; } = "normal";
///
///     [Description("Payment method")]
///     [McpAllowedValues("card", "cash", "mobile")]
///     public string PaymentMethod { get; set; } = "card";
/// }
/// </code>
/// This generates a JSON Schema with: <c>"enum": ["low", "normal", "high", "urgent"]</c>
/// </example>
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
