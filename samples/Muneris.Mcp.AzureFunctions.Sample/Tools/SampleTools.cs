using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Muneris.Mcp.AzureFunctions.Attributes;
using Muneris.Mcp.AzureFunctions.Models;

namespace Muneris.Mcp.AzureFunctions.Sample.Tools;

/// <summary>
/// Sample MCP tools demonstrating the attribute-based definition pattern.
/// </summary>
public sealed class SampleTools
{
    /// <summary>
    /// Echoes the provided message back to the caller.
    /// </summary>
    [McpTool("echo", Description = "Echoes back the provided message", AllowAnonymous = true, ReadOnlyHint = true, IdempotentHint = true)]
    [McpToolProperty("message", Type = "string", Description = "The message to echo back", Required = true)]
    public string Echo(string message)
    {
        return message;
    }

    /// <summary>
    /// Returns the current time in Denmark (Europe/Copenhagen timezone).
    /// Demonstrates a tool with annotations.
    /// </summary>
    [McpTool("get_denmark_time",
        Description = "Returns the current time in Denmark (Europe/Copenhagen timezone)",
        AllowAnonymous = true,
        Title = "Get Denmark Time",
        ReadOnlyHint = true,
        IdempotentHint = true,
        OpenWorldHint = false)]
    public object GetDenmarkTime()
    {
        var denmarkTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        var denmarkTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, denmarkTimeZone);

        return new
        {
            timezone = "Europe/Copenhagen",
            time = denmarkTime.ToString("yyyy-MM-dd HH:mm:ss"),
            iso8601 = denmarkTime.ToString("o")
        };
    }

    /// <summary>
    /// Returns information about the authenticated user.
    /// Requires authentication.
    /// </summary>
    [McpTool("get_user_info",
        Description = "Returns information about the authenticated user including email and claims",
        Title = "Get User Info",
        ReadOnlyHint = true)]
    public object GetUserInfo(ToolInvocationContext context)
    {
        var user = context.User;
        if (user is null)
        {
            return new { error = "No authenticated user" };
        }

        var claims = user.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList();
        var email = user.FindFirst("email")?.Value ??
                    user.FindFirst("preferred_username")?.Value ??
                    user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var name = user.FindFirst("name")?.Value ??
                   user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        return new
        {
            email,
            name,
            isAuthenticated = user.Identity?.IsAuthenticated ?? false,
            authenticationType = user.Identity?.AuthenticationType,
            claims
        };
    }

    /// <summary>
    /// Adds two numbers together.
    /// Demonstrates enhanced schema properties.
    /// </summary>
    [McpTool("add", Description = "Adds two numbers together and returns the result", AllowAnonymous = true, ReadOnlyHint = true)]
    [McpToolProperty("a", Type = "number", Description = "The first number", Required = true, Minimum = -1000000, Maximum = 1000000)]
    [McpToolProperty("b", Type = "number", Description = "The second number", Required = true, Minimum = -1000000, Maximum = 1000000)]
    public object Add(double a, double b)
    {
        return new { result = a + b };
    }

    /// <summary>
    /// Returns information about the current session.
    /// </summary>
    [McpTool("get_session_info", Description = "Returns information about the current MCP session", AllowAnonymous = true, ReadOnlyHint = true)]
    public object GetSessionInfo(ToolInvocationContext context)
    {
        return new
        {
            sessionId = context.SessionId,
            protocolVersion = context.ProtocolVersion,
            hasUser = context.User is not null,
            toolName = context.ToolName,
            requestMethod = context.Request.Method,
            requestUrl = context.Request.Url.ToString()
        };
    }

    /// <summary>
    /// Creates an order using POCO binding.
    /// Demonstrates automatic schema generation from DataAnnotations.
    /// </summary>
    [McpTool("create_order",
        Description = "Creates a new order with the specified details",
        AllowAnonymous = true,
        Title = "Create Order",
        DestructiveHint = false,
        IdempotentHint = false)]
    public object CreateOrder(ToolInvocationContext context, CreateOrderRequest request)
    {
        // In a real app, this would persist the order
        var orderId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        return new
        {
            orderId,
            status = "created",
            customerId = request.CustomerId,
            itemCount = request.Items?.Count ?? 0,
            priority = request.Priority,
            notes = request.Notes,
            createdAt = DateTime.UtcNow.ToString("O")
        };
    }

    /// <summary>
    /// Deletes an order.
    /// Demonstrates the destructive hint.
    /// </summary>
    [McpTool("delete_order",
        Description = "Deletes an order by ID (irreversible)",
        Title = "Delete Order",
        DestructiveHint = true,
        IdempotentHint = true)]
    [McpToolProperty("orderId", Type = "string", Description = "The order ID to delete", Required = true, Pattern = "^[A-Z0-9]{8}$")]
    public object DeleteOrder(ToolInvocationContext context, string orderId)
    {
        // In a real app, this would delete the order
        return new
        {
            orderId,
            status = "deleted",
            deletedAt = DateTime.UtcNow.ToString("O"),
            deletedBy = context.User?.FindFirst("email")?.Value ?? "anonymous"
        };
    }
}

/// <summary>
/// POCO request model for create_order tool.
/// Demonstrates DataAnnotations for automatic schema generation.
/// </summary>
public sealed class CreateOrderRequest
{
    [Description("The customer's unique identifier")]
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Description("Order line items")]
    [Required]
    public List<OrderItem> Items { get; set; } = new();

    [Description("Special instructions or notes")]
    [MaxLength(500)]
    public string? Notes { get; set; }

    [Description("Order priority level")]
    [McpAllowedValues("low", "normal", "high", "urgent")]
    public string Priority { get; set; } = "normal";
}

/// <summary>
/// Order item for the CreateOrderRequest.
/// </summary>
public sealed class OrderItem
{
    [Description("Menu item ID")]
    [Required]
    public string ItemId { get; set; } = string.Empty;

    [Description("Quantity to order")]
    [Required]
    [Range(1, 100)]
    public int Quantity { get; set; }

    [Description("Special modifications")]
    public string? Modifications { get; set; }
}
