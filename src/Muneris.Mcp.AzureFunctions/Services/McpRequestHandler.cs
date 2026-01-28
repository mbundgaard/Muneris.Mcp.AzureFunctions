using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muneris.Mcp.AzureFunctions.Auth;
using Muneris.Mcp.AzureFunctions.Extensions;
using Muneris.Mcp.AzureFunctions.Models;

namespace Muneris.Mcp.AzureFunctions.Services;

/// <summary>
/// Handles MCP JSON-RPC requests over Streamable HTTP transport.
/// </summary>
public sealed class McpRequestHandler
{
    private const string DefaultProtocolVersion = "2025-03-26";
    private const string SessionIdHeader = "Mcp-Session-Id";
    private const string ProtocolVersionHeader = "MCP-Protocol-Version";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly McpToolRegistry _toolRegistry;
    private readonly McpResourceRegistry? _resourceRegistry;
    private readonly IMcpAuthValidator? _authValidator;
    private readonly McpServerOptions _options;
    private readonly ILogger<McpRequestHandler> _logger;

    /// <summary>
    /// Creates a new MCP request handler.
    /// </summary>
    public McpRequestHandler(
        McpToolRegistry toolRegistry,
        IOptions<McpServerOptions> options,
        ILogger<McpRequestHandler> logger,
        McpResourceRegistry? resourceRegistry = null,
        IMcpAuthValidator? authValidator = null)
    {
        _toolRegistry = toolRegistry;
        _resourceRegistry = resourceRegistry;
        _authValidator = authValidator;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Handles an incoming HTTP request to the MCP endpoint.
    /// </summary>
    public async Task<HttpResponseData> HandleAsync(HttpRequestData request)
    {
        if (!ValidateOrigin(request))
        {
            return await CreateForbiddenResponse(request, "Invalid Origin");
        }

        return request.Method.ToUpperInvariant() switch
        {
            "POST" => await HandlePostAsync(request),
            // v1: JSON-only transport - GET returns 405 per MCP spec
            "GET" => CreateMethodNotAllowedResponse(request),
            // DELETE not needed for JSON-only transport
            "DELETE" => CreateMethodNotAllowedResponse(request),
            _ => CreateMethodNotAllowedResponse(request)
        };
    }

    private bool ValidateOrigin(HttpRequestData request)
    {
        if (_options.AllowedOrigins is null || _options.AllowedOrigins.Count == 0)
        {
            return true;
        }

        if (!request.Headers.TryGetValues("Origin", out var origins))
        {
            return true;
        }

        var origin = origins.FirstOrDefault();
        if (string.IsNullOrEmpty(origin))
        {
            return true;
        }

        return _options.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HttpResponseData> HandlePostAsync(HttpRequestData request)
    {
        JsonRpcRequest? rpcRequest;
        try
        {
            var body = await request.ReadAsStringAsync();
            if (string.IsNullOrEmpty(body))
            {
                return await CreateJsonRpcErrorResponse(request, null, JsonRpcErrorCodes.ParseError, "Empty request body");
            }

            rpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body, JsonOptions);
            if (rpcRequest is null)
            {
                return await CreateJsonRpcErrorResponse(request, null, JsonRpcErrorCodes.ParseError, "Failed to parse JSON-RPC request");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON-RPC request");
            return await CreateJsonRpcErrorResponse(request, null, JsonRpcErrorCodes.ParseError, "Invalid JSON");
        }

        if (rpcRequest.JsonRpc != "2.0")
        {
            return await CreateJsonRpcErrorResponse(request, rpcRequest.Id, JsonRpcErrorCodes.InvalidRequest, "Invalid JSON-RPC version");
        }

        if (string.IsNullOrEmpty(rpcRequest.Method))
        {
            return await CreateJsonRpcErrorResponse(request, rpcRequest.Id, JsonRpcErrorCodes.InvalidRequest, "Method is required");
        }

        var sessionId = GetSessionId(request);
        var protocolVersion = GetProtocolVersion(request);
        var user = await ValidateAuthAsync(request);

        _logger.LogDebug("Processing MCP method: {Method}", rpcRequest.Method);

        try
        {
            object? result = rpcRequest.Method switch
            {
                "initialize" => HandleInitialize(rpcRequest, ref sessionId),
                "notifications/initialized" => HandleInitialized(),
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolsCall(rpcRequest, request, user, sessionId, protocolVersion),
                "resources/list" => await HandleResourcesList(request, user, sessionId, protocolVersion),
                "resources/read" => await HandleResourcesRead(rpcRequest, request, user, sessionId, protocolVersion),
                "ping" => HandlePing(),
                _ => throw new McpMethodNotFoundException(rpcRequest.Method)
            };

            return await CreateJsonRpcSuccessResponse(request, rpcRequest.Id, result, sessionId);
        }
        catch (McpMethodNotFoundException)
        {
            return await CreateJsonRpcErrorResponse(request, rpcRequest.Id, JsonRpcErrorCodes.MethodNotFound,
                $"Method not found: {rpcRequest.Method}");
        }
        catch (McpAuthenticationException ex)
        {
            return await CreateAuthErrorResponse(request, rpcRequest.Id, ex.Message);
        }
        catch (McpInvalidParamsException ex)
        {
            return await CreateJsonRpcErrorResponse(request, rpcRequest.Id, JsonRpcErrorCodes.InvalidParams, ex.Message);
        }
        catch (McpResourceNotFoundException ex)
        {
            return await CreateJsonRpcErrorResponse(request, rpcRequest.Id, JsonRpcErrorCodes.ResourceNotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MCP method: {Method}", rpcRequest.Method);
            return await CreateJsonRpcErrorResponse(request, rpcRequest.Id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
    }

    private static string? GetSessionId(HttpRequestData request)
    {
        return request.Headers.TryGetValues(SessionIdHeader, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static string GetProtocolVersion(HttpRequestData request)
    {
        return request.Headers.TryGetValues(ProtocolVersionHeader, out var values)
            ? values.FirstOrDefault() ?? DefaultProtocolVersion
            : DefaultProtocolVersion;
    }

    private async Task<ClaimsPrincipal?> ValidateAuthAsync(HttpRequestData request)
    {
        if (_authValidator is null)
        {
            return null;
        }

        // Try async validation first
        var user = await _authValidator.ValidateRequestAsync(request);
        if (user is not null)
        {
            return user;
        }

        // Fall back to sync validation for backwards compatibility
        return _authValidator.ValidateRequest(request);
    }

    private object HandleInitialize(JsonRpcRequest request, ref string? sessionId)
    {
        var clientProtocolVersion = DefaultProtocolVersion;
        if (request.Params is not null &&
            request.Params.Value.TryGetProperty("protocolVersion", out var versionElement) &&
            versionElement.ValueKind == JsonValueKind.String)
        {
            clientProtocolVersion = versionElement.GetString() ?? DefaultProtocolVersion;
        }

        sessionId ??= Guid.NewGuid().ToString("N");

        // Build capabilities based on what's registered
        var capabilities = new Dictionary<string, object>
        {
            ["tools"] = new { listChanged = false }
        };

        // Only include resources capability if resources are registered
        if (_resourceRegistry?.HasAnyResources == true)
        {
            capabilities["resources"] = new { listChanged = false };
        }

        return new
        {
            protocolVersion = clientProtocolVersion,
            capabilities,
            serverInfo = new
            {
                name = _options.ServerName,
                version = _options.ServerVersion
            },
            instructions = _options.Instructions
        };
    }

    private static object? HandleInitialized()
    {
        return null;
    }

    private object HandleToolsList()
    {
        return new
        {
            tools = _toolRegistry.GetToolDefinitions()
        };
    }

    private async Task<object> HandleToolsCall(
        JsonRpcRequest request,
        HttpRequestData httpRequest,
        ClaimsPrincipal? user,
        string? sessionId,
        string? protocolVersion)
    {
        string? toolName = null;
        JsonElement? arguments = null;

        if (request.Params is not null && request.Params.Value.ValueKind == JsonValueKind.Object)
        {
            if (request.Params.Value.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                toolName = nameElement.GetString();
            }

            if (request.Params.Value.TryGetProperty("arguments", out var argsElement))
            {
                arguments = argsElement;
            }
        }

        if (string.IsNullOrEmpty(toolName))
        {
            throw new McpInvalidParamsException("Tool name is required");
        }

        if (!_toolRegistry.HasTool(toolName))
        {
            throw new McpInvalidParamsException($"Unknown tool: {toolName}");
        }

        if (!_toolRegistry.IsToolAnonymous(toolName) && user is null)
        {
            var error = _authValidator?.GetAuthError(httpRequest) ?? "Authentication required";
            throw new McpAuthenticationException(error);
        }

        var context = new ToolInvocationContext(
            toolName,
            arguments,
            user,
            sessionId,
            httpRequest,
            protocolVersion);

        _logger.LogInformation("Invoking tool: {ToolName}", toolName);

        var result = await _toolRegistry.InvokeToolAsync(context);

        // Handle McpToolResult directly
        if (result is McpToolResult toolResult)
        {
            return toolResult;
        }

        // Convert simple string result to content array
        if (result is string textResult)
        {
            return new
            {
                content = new[]
                {
                    new { type = "text", text = textResult }
                }
            };
        }

        // Serialize other results as JSON text
        var serialized = JsonSerializer.Serialize(result, JsonOptions);
        return new
        {
            content = new[]
            {
                new { type = "text", text = serialized }
            }
        };
    }

    private async Task<object> HandleResourcesList(
        HttpRequestData httpRequest,
        ClaimsPrincipal? user,
        string? sessionId,
        string? protocolVersion)
    {
        if (_resourceRegistry is null)
        {
            return new { resources = Array.Empty<object>() };
        }

        var resources = await _resourceRegistry.GetAllResourcesAsync(
            user,
            sessionId,
            protocolVersion,
            httpRequest);

        return new { resources };
    }

    private async Task<object> HandleResourcesRead(
        JsonRpcRequest request,
        HttpRequestData httpRequest,
        ClaimsPrincipal? user,
        string? sessionId,
        string? protocolVersion)
    {
        if (_resourceRegistry is null)
        {
            throw new McpResourceNotFoundException("Resources not enabled");
        }

        string? uri = null;

        if (request.Params is not null && request.Params.Value.ValueKind == JsonValueKind.Object)
        {
            if (request.Params.Value.TryGetProperty("uri", out var uriElement) &&
                uriElement.ValueKind == JsonValueKind.String)
            {
                uri = uriElement.GetString();
            }
        }

        if (string.IsNullOrEmpty(uri))
        {
            throw new McpInvalidParamsException("Resource URI is required");
        }

        if (!_resourceRegistry.HasResource(uri))
        {
            throw new McpResourceNotFoundException($"Resource not found: {uri}");
        }

        if (!_resourceRegistry.IsResourceAnonymous(uri) && user is null)
        {
            var error = _authValidator?.GetAuthError(httpRequest) ?? "Authentication required";
            throw new McpAuthenticationException(error);
        }

        _logger.LogInformation("Reading resource: {Uri}", uri);

        var result = await _resourceRegistry.ReadResourceAsync(
            uri,
            user,
            sessionId,
            protocolVersion,
            httpRequest);

        if (result is null)
        {
            throw new McpResourceNotFoundException($"Resource not found: {uri}");
        }

        return result;
    }

    private static object HandlePing()
    {
        return new { };
    }

    private async Task<HttpResponseData> CreateJsonRpcSuccessResponse(HttpRequestData request, JsonElement? id, object? result, string? sessionId)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        if (sessionId is not null)
        {
            response.Headers.Add(SessionIdHeader, sessionId);
        }

        var rpcResponse = JsonRpcResponse.Success(id, result);
        var json = JsonSerializer.Serialize(rpcResponse, JsonOptions);
        await response.WriteStringAsync(json);
        return response;
    }

    private async Task<HttpResponseData> CreateJsonRpcErrorResponse(HttpRequestData request, JsonElement? id, int code, string message)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        var rpcResponse = JsonRpcResponse.Failure(id, code, message);
        var json = JsonSerializer.Serialize(rpcResponse, JsonOptions);
        await response.WriteStringAsync(json);
        return response;
    }

    private async Task<HttpResponseData> CreateAuthErrorResponse(HttpRequestData request, JsonElement? id, string message)
    {
        var response = request.CreateResponse(HttpStatusCode.Unauthorized);
        response.Headers.Add("Content-Type", "application/json");

        var wwwAuth = _authValidator?.GetWwwAuthenticateHeader();
        if (!string.IsNullOrEmpty(wwwAuth))
        {
            response.Headers.Add("WWW-Authenticate", wwwAuth);
        }

        var rpcResponse = JsonRpcResponse.Failure(id, JsonRpcErrorCodes.AuthenticationError, message);
        var json = JsonSerializer.Serialize(rpcResponse, JsonOptions);
        await response.WriteStringAsync(json);
        return response;
    }

    private async Task<HttpResponseData> CreateForbiddenResponse(HttpRequestData request, string message)
    {
        var response = request.CreateResponse(HttpStatusCode.Forbidden);
        response.Headers.Add("Content-Type", "application/json");

        var rpcResponse = JsonRpcResponse.Failure(null, JsonRpcErrorCodes.InternalError, message);
        var json = JsonSerializer.Serialize(rpcResponse, JsonOptions);
        await response.WriteStringAsync(json);
        return response;
    }

    private HttpResponseData CreateMethodNotAllowedResponse(HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.MethodNotAllowed);
        response.Headers.Add("Allow", "POST");
        return response;
    }
}

internal sealed class McpMethodNotFoundException : Exception
{
    public McpMethodNotFoundException(string method) : base($"Method not found: {method}") { }
}

internal sealed class McpAuthenticationException : Exception
{
    public McpAuthenticationException(string message) : base(message) { }
}

internal sealed class McpInvalidParamsException : Exception
{
    public McpInvalidParamsException(string message) : base(message) { }
}

internal sealed class McpResourceNotFoundException : Exception
{
    public McpResourceNotFoundException(string message) : base(message) { }
}
