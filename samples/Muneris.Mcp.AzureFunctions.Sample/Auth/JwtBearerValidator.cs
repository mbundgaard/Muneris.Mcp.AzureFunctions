using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Muneris.Mcp.AzureFunctions.Auth;

namespace Muneris.Mcp.AzureFunctions.Sample.Auth;

/// <summary>
/// JWT Bearer token validator for MCP authentication.
/// Validates tokens issued by a configured identity provider.
/// </summary>
public sealed class JwtBearerValidator : IMcpAuthValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtBearerValidator> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private string? _lastError;

    public JwtBearerValidator(IConfiguration configuration, ILogger<JwtBearerValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<ClaimsPrincipal?> ValidateRequestAsync(HttpRequestData request, CancellationToken cancellationToken = default)
    {
        // This validator doesn't need async operations, so delegate to sync method
        return Task.FromResult(ValidateRequest(request));
    }

    public ClaimsPrincipal? ValidateRequest(HttpRequestData request)
    {
        _lastError = null;

        if (!request.Headers.TryGetValues("Authorization", out var authHeaders))
        {
            _lastError = "Missing Authorization header";
            return null;
        }

        var authHeader = authHeaders.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
        {
            _lastError = "Empty Authorization header";
            return null;
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _lastError = "Authorization header must use Bearer scheme";
            return null;
        }

        var token = authHeader.Substring(7).Trim();
        if (string.IsNullOrEmpty(token))
        {
            _lastError = "Bearer token is empty";
            return null;
        }

        try
        {
            var validationParameters = GetValidationParameters();
            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            _logger.LogDebug("Successfully validated JWT token");
            return principal;
        }
        catch (SecurityTokenExpiredException)
        {
            _lastError = "Token has expired";
            _logger.LogWarning("JWT token validation failed: token expired");
            return null;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _lastError = "Invalid token signature";
            _logger.LogWarning("JWT token validation failed: invalid signature");
            return null;
        }
        catch (SecurityTokenInvalidAudienceException)
        {
            _lastError = "Invalid token audience";
            _logger.LogWarning("JWT token validation failed: invalid audience");
            return null;
        }
        catch (SecurityTokenInvalidIssuerException)
        {
            _lastError = "Invalid token issuer";
            _logger.LogWarning("JWT token validation failed: invalid issuer");
            return null;
        }
        catch (Exception ex)
        {
            _lastError = "Token validation failed";
            _logger.LogWarning(ex, "JWT token validation failed");
            return null;
        }
    }

    public string GetAuthError(HttpRequestData request)
    {
        return _lastError ?? "Unauthorized";
    }

    public string? GetWwwAuthenticateHeader()
    {
        var realm = _configuration["Jwt:Realm"] ?? "mcp";
        return $"Bearer realm=\"{realm}\"";
    }

    private TokenValidationParameters GetValidationParameters()
    {
        var audience = _configuration["Jwt:Audience"];
        var validIssuers = _configuration["Jwt:ValidIssuers"]?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var authority = _configuration["Jwt:Authority"];

        return new TokenValidationParameters
        {
            ValidateIssuer = validIssuers is not null && validIssuers.Length > 0,
            ValidIssuers = validIssuers,
            ValidateAudience = !string.IsNullOrEmpty(audience),
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false,
            SignatureValidator = (token, parameters) =>
            {
                var jwt = new JwtSecurityToken(token);
                return jwt;
            },
            RequireSignedTokens = false
        };
    }
}
