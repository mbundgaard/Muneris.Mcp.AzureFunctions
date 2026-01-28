using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;

namespace Muneris.Mcp.AzureFunctions.Models.Resources;

/// <summary>
/// Context passed to MCP resource handlers during invocation.
/// Provides access to URI parameters, authentication info, and the underlying HTTP request.
/// </summary>
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
            throw new InvalidOperationException($"Required parameter '{name}' not found in URI");
        }
        return value;
    }
}
