namespace Muneris.Mcp.AzureFunctions.Attributes;

/// <summary>
/// Marks a method as a dynamic resource list provider for a URI scheme.
/// Use this when resources are discovered at runtime rather than statically defined.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpResourceListAttribute : Attribute
{
    /// <summary>
    /// Gets the URI scheme this method provides resources for (e.g., "menu", "config").
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// Creates a new MCP resource list attribute.
    /// </summary>
    /// <param name="scheme">The URI scheme (e.g., "menu" for "menu://" URIs).</param>
    public McpResourceListAttribute(string scheme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        Scheme = scheme;
    }
}
