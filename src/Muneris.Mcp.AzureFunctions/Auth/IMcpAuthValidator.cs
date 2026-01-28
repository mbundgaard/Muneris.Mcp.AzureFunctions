using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;

namespace Muneris.Mcp.AzureFunctions.Auth;

/// <summary>
/// Interface for validating MCP request authentication.
/// Implement this interface to provide custom authentication logic (JWT, API keys, Azure AD, etc.).
/// </summary>
/// <example>
/// <para><b>JWT Bearer token validator:</b></para>
/// <code>
/// public class JwtBearerValidator : IMcpAuthValidator
/// {
///     private readonly TokenValidationParameters _validationParameters;
///     private readonly JwtSecurityTokenHandler _tokenHandler = new();
///
///     public JwtBearerValidator(IConfiguration config)
///     {
///         _validationParameters = new TokenValidationParameters
///         {
///             ValidateIssuer = true,
///             ValidIssuer = config["Jwt:Issuer"],
///             ValidateAudience = true,
///             ValidAudience = config["Jwt:Audience"],
///             ValidateLifetime = true,
///             IssuerSigningKey = new SymmetricSecurityKey(
///                 Encoding.UTF8.GetBytes(config["Jwt:Secret"]!))
///         };
///     }
///
///     public Task&lt;ClaimsPrincipal?&gt; ValidateRequestAsync(
///         HttpRequestData request, CancellationToken cancellationToken = default)
///     {
///         if (!request.Headers.TryGetValues("Authorization", out var values))
///             return Task.FromResult&lt;ClaimsPrincipal?&gt;(null);
///
///         var authHeader = values.FirstOrDefault();
///         if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
///             return Task.FromResult&lt;ClaimsPrincipal?&gt;(null);
///
///         var token = authHeader.Substring("Bearer ".Length);
///         try
///         {
///             var principal = _tokenHandler.ValidateToken(token, _validationParameters, out _);
///             return Task.FromResult&lt;ClaimsPrincipal?&gt;(principal);
///         }
///         catch
///         {
///             return Task.FromResult&lt;ClaimsPrincipal?&gt;(null);
///         }
///     }
///
///     public string GetAuthError(HttpRequestData request) =&gt; "Invalid or expired token";
///     public string? GetWwwAuthenticateHeader() =&gt; "Bearer realm=\"mcp\"";
/// }
/// </code>
/// </example>
/// <example>
/// <para><b>API key validator:</b></para>
/// <code>
/// public class ApiKeyValidator : IMcpAuthValidator
/// {
///     private readonly HashSet&lt;string&gt; _validKeys;
///
///     public ApiKeyValidator(IConfiguration config)
///     {
///         _validKeys = config.GetSection("ApiKeys").Get&lt;string[]&gt;()?.ToHashSet()
///             ?? new HashSet&lt;string&gt;();
///     }
///
///     public Task&lt;ClaimsPrincipal?&gt; ValidateRequestAsync(
///         HttpRequestData request, CancellationToken cancellationToken = default)
///     {
///         if (!request.Headers.TryGetValues("X-API-Key", out var values))
///             return Task.FromResult&lt;ClaimsPrincipal?&gt;(null);
///
///         var apiKey = values.FirstOrDefault();
///         if (string.IsNullOrEmpty(apiKey) || !_validKeys.Contains(apiKey))
///             return Task.FromResult&lt;ClaimsPrincipal?&gt;(null);
///
///         var claims = new[] { new Claim(ClaimTypes.Name, "api-client") };
///         var identity = new ClaimsIdentity(claims, "ApiKey");
///         return Task.FromResult&lt;ClaimsPrincipal?&gt;(new ClaimsPrincipal(identity));
///     }
///
///     public string GetAuthError(HttpRequestData request) =&gt; "Invalid API key";
///     public string? GetWwwAuthenticateHeader() =&gt; null; // No challenge for API keys
/// }
/// </code>
/// </example>
/// <remarks>
/// Register your validator in Program.cs:
/// <code>
/// services.AddMcp(mcp =&gt; {
///     mcp.AddAuthValidator&lt;JwtBearerValidator&gt;();
///     // ... register tools and resources
/// });
/// </code>
/// </remarks>
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
