# Muneris.Mcp.AzureFunctions

[![NuGet](https://img.shields.io/nuget/v/Muneris.Mcp.AzureFunctions.svg)](https://www.nuget.org/packages/Muneris.Mcp.AzureFunctions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Build MCP (Model Context Protocol) servers on Azure Functions with a clean, attribute-based API.

## Why This Package?

Microsoft's `Microsoft.Azure.Functions.Worker.Extensions.Mcp` has limitations:

| | Microsoft | Muneris |
|---|-----------|---------|
| **Endpoint** | Hardcoded `/runtime/webhooks/mcp` | Configurable |
| **Transport** | SSE (deprecated March 2025) | Streamable HTTP |
| **Auth** | Azure AD / system keys only | Bring your own |
| **HTTP context** | Limited | Full access (headers, claims) |
| **Resources** | ❌ | ✅ |
| **POCO binding** | ❌ | ✅ |

## Installation

```bash
dotnet add package Muneris.Mcp.AzureFunctions
```

## Quick Start

### 1. Define a Tool

```csharp
public class MyTools
{
    [McpTool("get_weather", Description = "Get current weather for a location", AllowAnonymous = true)]
    [McpToolProperty("location", Type = "string", Description = "City name", Required = true)]
    public object GetWeather(string location)
    {
        // Your logic here
        return new { temperature = 22, unit = "celsius", location };
    }
}
```

### 2. Register in Program.cs

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddMcp(mcp =>
        {
            mcp.Configure(options =>
            {
                options.ServerName = "My MCP Server";
                options.ServerVersion = "1.0.0";
            });

            mcp.AddToolsFromType<MyTools>();
        });
    })
    .Build();

host.Run();
```

### 3. Add the Endpoint

```csharp
public class McpEndpoint
{
    private readonly McpRequestHandler _handler;

    public McpEndpoint(McpRequestHandler handler) => _handler = handler;

    [Function("Mcp")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mcp")]
        HttpRequestData request) => _handler.HandleAsync(request);

    [Function("McpGet")]
    public HttpResponseData Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mcp")]
        HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.MethodNotAllowed);
        response.Headers.Add("Allow", "POST");
        return response;
    }
}
```

That's it. Your MCP server is ready at `/api/mcp`.

## Features

### POCO Binding

Skip verbose attribute decorations—bind tool arguments to a class:

```csharp
public class CreateOrderRequest
{
    [Description("Customer ID")]
    [Required]
    public string CustomerId { get; set; } = "";

    [Description("Order items")]
    [Required]
    public List<OrderItem> Items { get; set; } = new();

    [Description("Priority level")]
    [McpAllowedValues("low", "normal", "high")]  // Or use .NET 8's [AllowedValues]
    public string Priority { get; set; } = "normal";

    [Description("Discount percentage")]
    [Range(0, 100)]
    public decimal? Discount { get; set; }
}

[McpTool("create_order", Description = "Create a new order")]
public object CreateOrder(ToolInvocationContext context, CreateOrderRequest request)
{
    // request is fully populated and validated
    return new { orderId = Guid.NewGuid().ToString("N")[..8], status = "created" };
}
```

JSON Schema is auto-generated from your C# types and DataAnnotations. Supports:
- `[Required]` → required array
- `[Description]` → description
- `[Range(min, max)]` → minimum/maximum
- `[MinLength]`, `[MaxLength]`, `[StringLength]` → minLength/maxLength
- `[RegularExpression]` → pattern
- `[McpAllowedValues]` or `[AllowedValues]` → enum
- `[EmailAddress]`, `[Url]` → format

### Resources

Expose read-only data with URI-based addressing:

```csharp
public class MenuResources
{
    [McpResource("menu://categories", "Menu Categories", Description = "List of menu categories", AllowAnonymous = true)]
    public McpResourceResult GetCategories(ResourceRequestContext context)
    {
        var data = new[] { "appetizers", "mains", "desserts" };
        return new McpResourceResult
        {
            Contents = new[] { McpResourceContentItem.FromText(context.Uri, "application/json", JsonSerializer.Serialize(data)) }
        };
    }

    [McpResource("menu://items/{category}", "Menu Items", Description = "Items in a category", AllowAnonymous = true)]
    public McpResourceResult GetItems(ResourceRequestContext context)
    {
        var category = context.GetRequiredParameter("category");
        var items = GetItemsForCategory(category);
        return new McpResourceResult
        {
            Contents = new[] { McpResourceContentItem.FromText(context.Uri, "application/json", JsonSerializer.Serialize(items)) }
        };
    }
}
```

### Pluggable Authentication

Implement `IMcpAuthValidator` for any auth scheme:

```csharp
public class JwtValidator : IMcpAuthValidator
{
    public Task<ClaimsPrincipal?> ValidateRequestAsync(HttpRequestData request, CancellationToken ct = default)
    {
        // Async validation (e.g., call external service)
        return Task.FromResult(ValidateRequest(request));
    }

    public ClaimsPrincipal? ValidateRequest(HttpRequestData request)
    {
        if (!request.Headers.TryGetValues("Authorization", out var values))
            return null;

        var token = values.FirstOrDefault()?.Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
            return null;

        // Your JWT validation logic
        return validatedPrincipal;
    }

    public string GetAuthError(HttpRequestData request) => "Invalid or missing token";
    public string? GetWwwAuthenticateHeader() => "Bearer";
}

// Register via builder
mcp.AddAuthValidator<JwtValidator>();

// Or standalone
services.AddMcpAuthValidator<JwtValidator>();
```

Control auth per-tool:

```csharp
[McpTool("public_tool", "Anyone can call this", AllowAnonymous = true)]
public Task<object> PublicTool(ToolInvocationContext context) { ... }

[McpTool("private_tool", "Requires authentication")]
public Task<object> PrivateTool(ToolInvocationContext context)
{
    var userId = context.User?.FindFirst("sub")?.Value;
    // ...
}
```

### Full HTTP Context

Access everything in your handlers:

```csharp
[McpTool("my_tool", Description = "Tool with full context")]
[McpToolProperty("myArg", Type = "string", Description = "An argument", Required = true)]
public object MyTool(ToolInvocationContext context, string myArg)
{
    // User claims (if authenticated)
    var userId = context.User?.FindFirst("sub")?.Value;

    // Session ID assigned by server
    var sessionId = context.SessionId;

    // Protocol version from client
    var protocolVersion = context.ProtocolVersion;

    // Raw HTTP request for custom headers
    var customHeader = context.Request.Headers.TryGetValues("X-Custom-Header", out var values)
        ? values.FirstOrDefault()
        : null;

    // Tool arguments via JsonElement
    var argFromJson = context.GetString("myArg");

    // Cancellation support
    context.CancellationToken.ThrowIfCancellationRequested();

    return new { success = true, userId, sessionId };
}
```

### Tool Annotations

Guide LLM behavior with hints:

```csharp
[McpTool("delete_order", "Delete an order permanently",
    Title = "Delete Order",
    DestructiveHint = true,      // LLM should confirm before calling
    IdempotentHint = true)]      // Safe to retry
public Task<object> DeleteOrder(ToolInvocationContext context, string orderId) { ... }

[McpTool("get_status", "Get order status",
    ReadOnlyHint = true)]        // Doesn't modify state
public Task<object> GetStatus(ToolInvocationContext context, string orderId) { ... }
```

## Configuration

```csharp
services.AddMcp(mcp =>
{
    mcp.Configure(options =>
    {
        // Server identity
        options.ServerName = "My Server";
        options.ServerVersion = "1.0.0";
        options.Instructions = "Use these tools to manage orders";

        // Security (recommended for production)
        options.AllowedOrigins = new List<string> { "https://claude.ai", "https://your-app.com" };

        // Protocol versions
        options.SupportedProtocolVersions = new[] { "2025-11-25", "2025-03-26" };

        // Features
        options.EnableTools = true;
        options.EnableResources = true;
    });

    mcp.AddToolsFromType<MyTools>();
    mcp.AddResourcesFromType<MyResources>();
    mcp.AddAuthValidator<MyAuthValidator>();
});
```

## MCP Specification Compliance

This package implements **Streamable HTTP transport** per [MCP spec 2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports).

v1 uses JSON-only responses (server always returns `application/json`). This is fully spec-compliant and optimized for serverless:

| Method | Response |
|--------|----------|
| POST | `application/json` |
| GET | `405 Method Not Allowed` |

For SSE streaming (progress notifications, real-time updates), see [v2 Roadmap](docs/v2-roadmap.md).

## Testing

Test with curl:

```bash
# Initialize session
curl -X POST http://localhost:7071/api/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'

# List tools (include session ID from initialize response)
curl -X POST http://localhost:7071/api/mcp \
  -H "Content-Type: application/json" \
  -H "Mcp-Session-Id: <session-id>" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'

# Call a tool
curl -X POST http://localhost:7071/api/mcp \
  -H "Content-Type: application/json" \
  -H "Mcp-Session-Id: <session-id>" \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_weather","arguments":{"location":"Copenhagen"}}}'
```

Or use MCP clients:
- [Claude Desktop](https://claude.ai/download)
- [VS Code MCP Extension](https://marketplace.visualstudio.com/items?itemName=anthropics.mcp)

## Documentation

- [v1 Specification](docs/Muneris.Mcp.AzureFunctions_v1_Specification.md) - Full API reference
- [v2 Roadmap](docs/Muneris.Mcp.AzureFunctions_v2_Roadmap.md) - SSE, prompts, progress, middleware
- [Sample Project](samples/Muneris.Mcp.AzureFunctions.Sample/) - Working example with tools, resources, and JWT auth

## Requirements

- .NET 8.0+
- Azure Functions (Isolated Worker) 2.0+

## License

MIT

## Contributing

Contributions welcome! Please read our [Contributing Guide](CONTRIBUTING.md) before submitting PRs.

---

Built by [Muneris ApS](https://muneris.dk) 🇩🇰
