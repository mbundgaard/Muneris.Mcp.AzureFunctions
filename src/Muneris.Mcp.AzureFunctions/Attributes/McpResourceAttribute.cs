namespace Muneris.Mcp.AzureFunctions.Attributes;

/// <summary>
/// Marks a method as an MCP resource that can be read by MCP clients.
/// Resources expose read-only data with URI-based addressing.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpResourceAttribute : Attribute
{
    /// <summary>
    /// Gets the URI pattern for this resource.
    /// Supports parameters in curly braces, e.g., "menu://categories/{categoryId}".
    /// </summary>
    public string UriPattern { get; }

    /// <summary>
    /// Gets the human-readable name of the resource.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the description of what this resource provides.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the resource content.
    /// Default is "application/json".
    /// </summary>
    public string MimeType { get; set; } = "application/json";

    /// <summary>
    /// Gets or sets whether this resource can be accessed without authentication.
    /// Default is false, meaning authentication is required.
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// Creates a new MCP resource attribute.
    /// </summary>
    /// <param name="uriPattern">The URI pattern for this resource (e.g., "menu://items/{id}").</param>
    /// <param name="name">The human-readable name of the resource.</param>
    public McpResourceAttribute(string uriPattern, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriPattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        UriPattern = uriPattern;
        Name = name;
    }
}
