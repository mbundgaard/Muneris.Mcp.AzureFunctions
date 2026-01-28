namespace Muneris.Mcp.AzureFunctions.Attributes;

/// <summary>
/// Marks a method as an MCP resource that can be read by MCP clients.
/// Resources expose read-only data with URI-based addressing.
/// </summary>
/// <example>
/// <para><b>Static resource (no parameters):</b></para>
/// <code>
/// [McpResource("config://server", "Server Configuration",
///     Description = "Returns server configuration", AllowAnonymous = true)]
/// public McpResourceResult GetServerConfig(ResourceRequestContext ctx)
/// {
///     var config = new { version = "1.0", environment = "production" };
///     return new McpResourceResult
///     {
///         Contents = new[] { McpResourceContentItem.FromJson(ctx.Uri, config) }
///     };
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>Dynamic resource with URI parameter:</b></para>
/// <code>
/// [McpResource("menu://categories/{categoryId}", "Menu Category",
///     Description = "Returns menu items for a category")]
/// public async Task&lt;McpResourceResult&gt; GetCategory(ResourceRequestContext ctx)
/// {
///     var categoryId = ctx.GetRequiredParameter("categoryId");
///     var items = await _menuService.GetItemsByCategoryAsync(categoryId, ctx.CancellationToken);
///
///     return new McpResourceResult
///     {
///         Contents = new[] { McpResourceContentItem.FromJson(ctx.Uri, items) }
///     };
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>Resource with multiple URI parameters:</b></para>
/// <code>
/// [McpResource("orders://{customerId}/{orderId}", "Customer Order",
///     Description = "Returns a specific order for a customer")]
/// public async Task&lt;McpResourceResult&gt; GetOrder(ResourceRequestContext ctx)
/// {
///     var customerId = ctx.GetRequiredParameter("customerId");
///     var orderId = ctx.GetRequiredParameter("orderId");
///
///     // Verify the authenticated user can access this customer's data
///     if (ctx.User?.FindFirst("customer_id")?.Value != customerId)
///         throw new UnauthorizedAccessException("Cannot access other customer's orders");
///
///     var order = await _orderService.GetAsync(customerId, orderId);
///     return new McpResourceResult
///     {
///         Contents = new[] { McpResourceContentItem.FromJson(ctx.Uri, order) }
///     };
/// }
/// </code>
/// </example>
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
