using System.Security.Claims;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace Muneris.Mcp.AzureFunctions.Models;

/// <summary>
/// Context passed to MCP tool handlers during invocation.
/// Provides access to arguments, authentication info, and the underlying HTTP request.
/// </summary>
/// <example>
/// <para><b>Accessing tool arguments:</b></para>
/// <code>
/// [McpTool("process_order", Description = "Processes an order")]
/// public async Task&lt;string&gt; ProcessOrder(ToolInvocationContext ctx)
/// {
///     var orderId = ctx.GetString("orderId") ?? throw new ArgumentException("orderId required");
///     var quantity = ctx.GetInt32("quantity") ?? 1;
///     var expedite = ctx.GetBoolean("expedite") ?? false;
///
///     // Get complex objects
///     var options = ctx.GetValue&lt;ProcessingOptions&gt;("options");
///
///     return $"Processed order {orderId}";
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>Accessing authenticated user information:</b></para>
/// <code>
/// [McpTool("get_profile", Description = "Gets the current user's profile")]
/// public async Task&lt;object&gt; GetProfile(ToolInvocationContext ctx)
/// {
///     // Get user claims from the authenticated principal
///     var userId = ctx.User?.FindFirst("sub")?.Value
///         ?? throw new UnauthorizedAccessException("Not authenticated");
///     var email = ctx.User?.FindFirst("email")?.Value;
///     var roles = ctx.User?.FindAll("role").Select(c =&gt; c.Value).ToList();
///
///     return new { userId, email, roles };
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>Using session and protocol information:</b></para>
/// <code>
/// [McpTool("debug_info", Description = "Returns debug information", AllowAnonymous = true)]
/// public object GetDebugInfo(ToolInvocationContext ctx)
/// {
///     return new
///     {
///         sessionId = ctx.SessionId,
///         protocolVersion = ctx.ProtocolVersion,
///         isAuthenticated = ctx.User?.Identity?.IsAuthenticated ?? false,
///         clientIp = ctx.Request.Headers.TryGetValues("X-Forwarded-For", out var ips)
///             ? ips.FirstOrDefault() : null
///     };
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>Using cancellation token for long-running operations:</b></para>
/// <code>
/// [McpTool("generate_report", Description = "Generates a detailed report")]
/// public async Task&lt;McpToolResult&gt; GenerateReport(ToolInvocationContext ctx, ReportRequest request)
/// {
///     // Pass cancellation token to async operations
///     var data = await _dataService.FetchDataAsync(request.DateRange, ctx.CancellationToken);
///
///     // Check for cancellation periodically in long operations
///     foreach (var item in data)
///     {
///         ctx.CancellationToken.ThrowIfCancellationRequested();
///         await ProcessItemAsync(item, ctx.CancellationToken);
///     }
///
///     return McpToolResult.Success("Report generated successfully");
/// }
/// </code>
/// </example>
public sealed class ToolInvocationContext
{
    /// <summary>
    /// The name of the tool being invoked.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// The arguments passed to the tool, deserialized from JSON.
    /// </summary>
    public JsonElement? Arguments { get; }

    /// <summary>
    /// The authenticated user, if any. Null for anonymous requests.
    /// </summary>
    public ClaimsPrincipal? User { get; }

    /// <summary>
    /// The MCP session ID, if provided by the client.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// The MCP protocol version from the client.
    /// </summary>
    public string? ProtocolVersion { get; }

    /// <summary>
    /// The underlying HTTP request for accessing headers and other request data.
    /// </summary>
    public HttpRequestData Request { get; }

    /// <summary>
    /// The cancellation token for the request.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new tool invocation context.
    /// </summary>
    public ToolInvocationContext(
        string toolName,
        JsonElement? arguments,
        ClaimsPrincipal? user,
        string? sessionId,
        HttpRequestData request,
        string? protocolVersion = null,
        CancellationToken cancellationToken = default)
    {
        ToolName = toolName;
        Arguments = arguments;
        User = user;
        SessionId = sessionId;
        ProtocolVersion = protocolVersion;
        Request = request;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets a string argument by name.
    /// </summary>
    public string? GetString(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (Arguments.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    /// <summary>
    /// Gets an integer argument by name.
    /// </summary>
    public int? GetInt32(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (Arguments.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number)
            return value.GetInt32();

        return null;
    }

    /// <summary>
    /// Gets a boolean argument by name.
    /// </summary>
    public bool? GetBoolean(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (Arguments.Value.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
        }

        return null;
    }

    /// <summary>
    /// Gets an argument deserialized to a specific type.
    /// </summary>
    public T? GetValue<T>(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return default;

        if (Arguments.Value.TryGetProperty(name, out var value))
            return JsonSerializer.Deserialize<T>(value.GetRawText());

        return default;
    }

    /// <summary>
    /// Checks if an argument exists.
    /// </summary>
    public bool HasArgument(string name)
    {
        if (Arguments is null || Arguments.Value.ValueKind != JsonValueKind.Object)
            return false;

        return Arguments.Value.TryGetProperty(name, out _);
    }
}
