using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;

namespace Muneris.Mcp.AzureFunctions.Models.Resources;

/// <summary>
/// Context passed to MCP resource handlers during invocation.
/// Provides access to URI parameters, authentication info, and the underlying HTTP request.
/// </summary>
/// <example>
/// <para><b>Accessing URI parameters:</b></para>
/// <code>
/// [McpResource("menu://categories/{categoryId}/items/{itemId}", "Menu Item")]
/// public async Task&lt;McpResourceResult&gt; GetMenuItem(ResourceRequestContext ctx)
/// {
///     // GetRequiredParameter throws if not found
///     var categoryId = ctx.GetRequiredParameter("categoryId");
///     var itemId = ctx.GetRequiredParameter("itemId");
///
///     // GetParameter returns null if not found
///     var version = ctx.GetParameter("version");
///
///     var item = await _menuService.GetItemAsync(categoryId, itemId);
///     return new McpResourceResult
///     {
///         Contents = new[] { McpResourceContentItem.FromJson(ctx.Uri, item) }
///     };
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>Iterating all parameters:</b></para>
/// <code>
/// [McpResource("search://{type}", "Search Results")]
/// public async Task&lt;McpResourceResult&gt; Search(ResourceRequestContext ctx)
/// {
///     var type = ctx.GetRequiredParameter("type");
///
///     // Log all parameters for debugging
///     foreach (var (name, value) in ctx.Parameters)
///     {
///         _logger.LogDebug("Parameter: {Name} = {Value}", name, value);
///     }
///
///     // Full URI is also available
///     _logger.LogInformation("Reading resource: {Uri}", ctx.Uri);
///
///     var results = await _searchService.SearchAsync(type);
///     return new McpResourceResult
///     {
///         Contents = new[] { McpResourceContentItem.FromJson(ctx.Uri, results) }
///     };
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>Authorization check with user context:</b></para>
/// <code>
/// [McpResource("users://{userId}/settings", "User Settings")]
/// public async Task&lt;McpResourceResult&gt; GetUserSettings(ResourceRequestContext ctx)
/// {
///     var requestedUserId = ctx.GetRequiredParameter("userId");
///     var currentUserId = ctx.User?.FindFirst("sub")?.Value;
///
///     // Users can only access their own settings unless admin
///     var isAdmin = ctx.User?.IsInRole("admin") ?? false;
///     if (!isAdmin &amp;&amp; currentUserId != requestedUserId)
///     {
///         throw new UnauthorizedAccessException("Cannot access other user's settings");
///     }
///
///     var settings = await _settingsService.GetAsync(requestedUserId, ctx.CancellationToken);
///     return new McpResourceResult
///     {
///         Contents = new[] { McpResourceContentItem.FromJson(ctx.Uri, settings) }
///     };
/// }
/// </code>
/// </example>
public sealed class ResourceRequestContext
{
    /// <summary>
    /// Gets the full URI of the resource being requested.
    /// </summary>
    public string Uri { get; }

    /// <summary>
    /// Gets the parameters extracted from the URI pattern.
    /// For example, if the pattern is "menu://categories/{categoryId}" and the URI is
    /// "menu://categories/appetizers", this will contain {"categoryId": "appetizers"}.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Gets the authenticated user, if any. Null for anonymous requests.
    /// </summary>
    public ClaimsPrincipal? User { get; }

    /// <summary>
    /// Gets the MCP session ID, if provided by the client.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// Gets the protocol version from the client.
    /// </summary>
    public string? ProtocolVersion { get; }

    /// <summary>
    /// Gets the underlying HTTP request for accessing headers and other request data.
    /// </summary>
    public HttpRequestData Request { get; }

    /// <summary>
    /// Gets the cancellation token for the request.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new resource request context.
    /// </summary>
    public ResourceRequestContext(
        string uri,
        IReadOnlyDictionary<string, string> parameters,
        ClaimsPrincipal? user,
        string? sessionId,
        string? protocolVersion,
        HttpRequestData request,
        CancellationToken cancellationToken = default)
    {
        Uri = uri;
        Parameters = parameters;
        User = user;
        SessionId = sessionId;
        ProtocolVersion = protocolVersion;
        Request = request;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets a parameter value from the URI, or null if not found.
    /// </summary>
    public string? GetParameter(string name)
    {
        return Parameters.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// Gets a required parameter value from the URI.
    /// Throws if the parameter is not found.
    /// </summary>
    public string GetRequiredParameter(string name)
    {
        if (!Parameters.TryGetValue(name, out var value))
        {
            throw new InvalidOperationException(
                $"Required parameter '{name}' not found in URI '{Uri}'. " +
                $"Available parameters: [{string.Join(", ", Parameters.Keys)}]. " +
                "Ensure the parameter name matches the placeholder in your [McpResource] URI pattern.");
        }
        return value;
    }
}
