using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Muneris.Mcp.AzureFunctions.Services;

namespace Muneris.Mcp.AzureFunctions.Sample;

/// <summary>
/// Azure Function HTTP trigger for the MCP endpoint.
/// v1 uses JSON-only transport (POST only, no SSE streaming).
/// </summary>
public sealed class McpEndpoint
{
    private readonly McpRequestHandler _handler;

    public McpEndpoint(McpRequestHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Handles POST requests containing JSON-RPC messages.
    /// </summary>
    [Function("Mcp")]
    public Task<HttpResponseData> HandleMcp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mcp")]
        HttpRequestData request)
    {
        return _handler.HandleAsync(request);
    }

    /// <summary>
    /// Returns 405 for GET requests per MCP v1 spec (no SSE streaming in v1).
    /// </summary>
    [Function("McpGet")]
    public HttpResponseData HandleMcpGet(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mcp")]
        HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.MethodNotAllowed);
        response.Headers.Add("Allow", "POST");
        return response;
    }
}
