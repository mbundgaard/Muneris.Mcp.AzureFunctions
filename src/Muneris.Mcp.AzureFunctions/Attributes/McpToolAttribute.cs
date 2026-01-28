namespace Muneris.Mcp.AzureFunctions.Attributes;

/// <summary>
/// Marks a method as an MCP tool that can be invoked by MCP clients.
/// </summary>
/// <example>
/// <para><b>Simple tool with attribute-based parameters:</b></para>
/// <code>
/// [McpTool("greet", Description = "Greets a user by name")]
/// [McpToolProperty("name", Type = "string", Description = "Name to greet", Required = true)]
/// public string Greet(string name) =&gt; $"Hello, {name}!";
/// </code>
/// </example>
/// <example>
/// <para><b>Tool with POCO binding (recommended for complex inputs):</b></para>
/// <code>
/// [McpTool("create_order", Description = "Creates a new order", Title = "Create Order")]
/// public async Task&lt;McpToolResult&gt; CreateOrder(ToolInvocationContext ctx, CreateOrderRequest request)
/// {
///     var userId = ctx.User?.FindFirst("sub")?.Value;
///     // Process order...
///     return McpToolResult.Success($"Order {orderId} created");
/// }
///
/// public class CreateOrderRequest
/// {
///     [Description("Customer ID")]
///     [Required]
///     public string CustomerId { get; set; } = "";
///
///     [Description("Order items")]
///     [Required]
///     public List&lt;OrderItem&gt; Items { get; set; } = new();
///
///     [Description("Priority level")]
///     [McpAllowedValues("low", "normal", "high")]
///     public string Priority { get; set; } = "normal";
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>Anonymous tool (no authentication required):</b></para>
/// <code>
/// [McpTool("get_menu", Description = "Gets the restaurant menu", AllowAnonymous = true, ReadOnlyHint = true)]
/// public async Task&lt;object&gt; GetMenu()
/// {
///     return await _menuService.GetMenuAsync();
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>Tool with behavioral hints:</b></para>
/// <code>
/// [McpTool("delete_order",
///     Description = "Cancels and deletes an order",
///     Title = "Delete Order",
///     DestructiveHint = true,    // Warns LLM this is destructive
///     IdempotentHint = true,     // Safe to retry
///     OpenWorldHint = true)]     // Interacts with external systems
/// public async Task&lt;string&gt; DeleteOrder(ToolInvocationContext ctx, string orderId)
/// {
///     await _orderService.DeleteAsync(orderId, ctx.CancellationToken);
///     return $"Order {orderId} deleted";
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the tool as exposed to MCP clients.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the description of what the tool does.
    /// This is shown to MCP clients to help them understand the tool's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether the tool can be invoked without authentication.
    /// Default is false, meaning authentication is required.
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// Gets or sets the human-readable title for the tool.
    /// Used as a hint for LLMs and UI display.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets whether this tool only reads data without modifying state.
    /// Default is false. When true, hints to LLMs that the tool is safe to call without side effects.
    /// </summary>
    public bool ReadOnlyHint { get; set; }

    /// <summary>
    /// Gets or sets whether this tool performs destructive operations (e.g., delete, cancel).
    /// Default is false. When true, hints to LLMs to use caution before invoking.
    /// </summary>
    public bool DestructiveHint { get; set; }

    /// <summary>
    /// Gets or sets whether this tool is idempotent (safe to call multiple times).
    /// Default is false. When true, hints to LLMs that repeated calls are safe.
    /// </summary>
    public bool IdempotentHint { get; set; }

    /// <summary>
    /// Gets or sets whether this tool interacts with external/open-world systems.
    /// Default is true. When true, indicates the tool may access external services.
    /// </summary>
    public bool OpenWorldHint { get; set; } = true;

    /// <summary>
    /// Creates a new MCP tool attribute.
    /// </summary>
    /// <param name="name">The name of the tool as exposed to MCP clients.</param>
    public McpToolAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
