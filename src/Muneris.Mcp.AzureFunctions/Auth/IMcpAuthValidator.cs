using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;

namespace Muneris.Mcp.AzureFunctions.Auth;

/// <summary>
/// Interface for validating MCP request authentication.
/// Implement this interface to provide custom authentication logic (JWT, API keys, Azure AD, etc.).
/// </summary>
public interface IMcpAuthValidator
{
    /// <summary>
    /// Validates the incoming HTTP request asynchronously and returns a ClaimsPrincipal if valid.
    /// Return null if authentication fails or no credentials are provided.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ClaimsPrincipal representing the authenticated user, or null if authentication fails.</returns>
    Task<ClaimsPrincipal?> ValidateRequestAsync(HttpRequestData request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the incoming HTTP request synchronously and returns a ClaimsPrincipal if valid.
    /// Return null if authentication fails or no credentials are provided.
    /// This is a convenience method for simple validators that don't need async.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <returns>A ClaimsPrincipal representing the authenticated user, or null if authentication fails.</returns>
    ClaimsPrincipal? ValidateRequest(HttpRequestData request) => null;

    /// <summary>
    /// Gets the error message to return when authentication fails.
    /// Override to provide custom error messages.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <returns>An error message describing why authentication failed.</returns>
    string GetAuthError(HttpRequestData request) => "Unauthorized";

    /// <summary>
    /// Gets the WWW-Authenticate header value to return on 401 responses.
    /// Override to customize the authentication challenge.
    /// </summary>
    /// <returns>The WWW-Authenticate header value, or null to omit the header.</returns>
    string? GetWwwAuthenticateHeader() => "Bearer";
}
