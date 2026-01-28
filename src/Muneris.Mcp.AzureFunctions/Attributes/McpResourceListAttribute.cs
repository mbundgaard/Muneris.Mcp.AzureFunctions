namespace Muneris.Mcp.AzureFunctions.Attributes;

/// <summary>
/// Marks a method as a dynamic resource list provider for a URI scheme.
/// Use this when resources are discovered at runtime rather than statically defined.
/// </summary>
/// <example>
/// <para><b>List resources from a database:</b></para>
/// <code>
/// [McpResourceList("menu")]
/// public async Task&lt;IEnumerable&lt;McpResourceInfo&gt;&gt; ListMenuResources(ResourceRequestContext ctx)
/// {
///     var categories = await _menuService.GetCategoriesAsync(ctx.CancellationToken);
///
///     return categories.Select(c =&gt; new McpResourceInfo
///     {
///         Uri = $"menu://categories/{c.Id}",
///         Name = c.Name,
///         Description = $"Menu items in {c.Name} category",
///         MimeType = "application/json"
///     });
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>List user-specific resources:</b></para>
/// <code>
/// [McpResourceList("orders")]
/// public async Task&lt;IEnumerable&lt;McpResourceInfo&gt;&gt; ListUserOrders(ResourceRequestContext ctx)
/// {
///     var userId = ctx.User?.FindFirst("sub")?.Value
///         ?? throw new UnauthorizedAccessException("Authentication required");
///
///     var orders = await _orderService.GetOrdersForUserAsync(userId, ctx.CancellationToken);
///
///     return orders.Select(o =&gt; new McpResourceInfo
///     {
///         Uri = $"orders://{userId}/{o.Id}",
///         Name = $"Order #{o.Id}",
///         Description = $"Order placed on {o.CreatedAt:d}",
///         MimeType = "application/json"
///     });
/// }
/// </code>
/// </example>
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
