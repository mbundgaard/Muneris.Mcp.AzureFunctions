# Muneris.Mcp.AzureFunctions

## Objective

Build a NuGet package (`Muneris.Mcp.AzureFunctions`) that enables Azure Functions to serve as MCP (Model Context Protocol) servers with:

- **Streamable HTTP transport** (MCP spec 2025-11-25)
- **Attribute-based tool and resource definitions** (developer-friendly)
- **POCO binding for tool parameters** (clean DX, auto-generated JSON Schema)
- **Pluggable authentication** (bring your own validator - JWT, API keys, Azure AD, etc.)
- **Configurable endpoint** (default `/mcp`, not hardcoded)
- **Full HTTP context access** in handlers (headers, claims, session ID)

This package addresses limitations in Microsoft's `Microsoft.Azure.Functions.Worker.Extensions.Mcp`:

| Limitation | Microsoft | Muneris |
|------------|-----------|---------|
| Endpoint | Hardcoded `/runtime/webhooks/mcp` | Configurable |
| Transport | SSE (deprecated) | Streamable HTTP |
| Auth | Azure AD / system keys only | Pluggable `IMcpAuthValidator` |
| HTTP context | Limited | Full access in handlers |
| Resources | Not supported | Full support |

---

## Project Documentation

| Document | Purpose |
|----------|---------|
| `Muneris.Mcp.AzureFunctions_v1_Specification.md` | Production-ready core: tools, resources, auth, JSON-only transport |
| `Muneris.Mcp.AzureFunctions_v2_Roadmap.md` | Advanced features: SSE streaming, prompts, progress, middleware |
| `MCP_SSE_Deprecation_Research.md` | Background on why SSE was deprecated |

---

## Transport Design

### The Spec Allows Choice

MCP Streamable HTTP (2025-11-25) gives servers flexibility:

| Endpoint | Spec Requirement | Options |
|----------|------------------|---------|
| POST | Server MUST return `application/json` OR `text/event-stream` | Server chooses per-request |
| GET | Server MUST return `text/event-stream` OR `405 Method Not Allowed` | Server chooses |

### Our Approach

**v1: JSON-only transport**
- POST returns `application/json`
- GET returns `405 Method Not Allowed`
- Fully spec-compliant
- Optimized for Azure Functions Consumption plan (no long-lived connections)
- Simple, stateless, serverless-friendly

**v2: Full SSE support**
- POST can return `text/event-stream` for streaming scenarios
- GET returns SSE stream for server-initiated messages
- Enables progress notifications, real-time updates
- Better for Premium/Dedicated plans

Both versions are spec-compliant. Choose based on deployment constraints:

| Plan | Recommendation |
|------|----------------|
| Consumption | v1 (JSON-only) |
| Premium | v2 (SSE enabled) |
| Dedicated / Container Apps | v2 (SSE enabled) |

---

## MCP Specification References

### Core Specification (2025-11-25)

| Document | URL | Key Topics |
|----------|-----|------------|
| **Transports** | https://modelcontextprotocol.io/specification/2025-11-25/basic/transports | Streamable HTTP, session handling, Origin validation |
| **Authorization** | https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization | OAuth 2.1 resource server role, token validation |
| **Tools** | https://modelcontextprotocol.io/specification/2025-11-25/server/tools | Tool definitions, input schemas, annotations |
| **Resources** | https://modelcontextprotocol.io/specification/2025-11-25/server/resources | Resource URIs, templates, MIME types |
| **Prompts** | https://modelcontextprotocol.io/specification/2025-11-25/server/prompts | Prompt templates (v2) |

### Reference Implementations

| Resource | URL | Purpose |
|----------|-----|---------|
| TypeScript SDK | https://github.com/modelcontextprotocol/typescript-sdk | Reference patterns |
| SDK server docs | https://github.com/modelcontextprotocol/typescript-sdk/blob/main/docs/server.md | Stateless patterns, DNS rebinding |

---

## v1 Features Summary

### Tools
- `[McpTool]` attribute with name, description, annotations (hints)
  - `Title` - Human-readable title
  - `ReadOnlyHint` - Tool only reads data (default: false)
  - `DestructiveHint` - Tool performs destructive operations (default: false)
  - `IdempotentHint` - Safe to call multiple times (default: false)
  - `OpenWorldHint` - Interacts with external systems (default: true)
- `[McpToolProperty]` for individual parameters with schema constraints:
  - `Enum`, `Format`, `Minimum`, `Maximum`, `MinLength`, `MaxLength`, `Pattern`, `Default`
- **POCO binding**: Bind tool arguments to a class using `[Description]`, `[Required]`, `[Range]`, `[AllowedValues]`, etc.
- Auto-generated JSON Schema from C# types and DataAnnotations
- `ToolInvocationContext` with full HTTP context, user claims, session ID, protocol version, cancellation token

### Resources
- `[McpResource]` with URI patterns (e.g., `menu://items/{category}`)
- `[McpResourceList]` for dynamic resource discovery
- `ResourceRequestContext` similar to tools

### Authentication
- `IMcpAuthValidator` interface for pluggable auth (async `ValidateRequestAsync` + sync `ValidateRequest`)
- Per-tool/resource `AllowAnonymous` attribute
- Sample `JwtBearerValidator` in sample project

### Content Types
- `McpToolResult` for structured tool responses
- `McpTextContent`, `McpImageContent`, `McpResourceContent` for typed content
- Tools can return strings (auto-wrapped) or `McpToolResult` for full control

### Transport
- Single `/mcp` endpoint (configurable)
- POST for all JSON-RPC messages
- GET returns 405
- Origin header validation (security critical)
- Session ID management via `Mcp-Session-Id` header

---

## v2 Features Summary

### Transport
- SSE response format for POST (streaming progress, notifications)
- GET SSE endpoint for server-initiated messages
- Resumability via `Last-Event-ID` (optional)

### Protocol Features
- **Prompts**: Predefined templates for LLM interactions
- **Progress**: Real-time updates during long operations
- **Cancellation**: Cancel in-flight requests
- **Logging**: Structured server-to-client logs
- **Resource subscriptions**: Real-time change notifications
- **Server Cards**: `/.well-known/mcp.json` capability discovery
- **OAuth metadata**: `/.well-known/oauth-protected-resource`

### Infrastructure
- **Fluent tool definition**: Runtime tool registration
- **Session state store**: Stateful workflows
- **Tool middleware**: Logging, metrics, rate limiting, validation

---

## Critical Implementation Requirements

### 1. Origin Header Validation (Security)

Per spec: "Servers MUST validate the Origin header on all incoming connections to prevent DNS rebinding attacks."

```csharp
if (originHeader != null && !IsAllowedOrigin(originHeader))
{
    return request.CreateResponse(HttpStatusCode.Forbidden);
}
```

### 2. Session Management

- Server assigns `Mcp-Session-Id` on initialize response
- Client includes it on all subsequent requests
- Session ID: globally unique, cryptographically secure, visible ASCII only (0x21-0x7E)

### 3. Protocol Version

- Client sends `MCP-Protocol-Version` header
- Default to `2025-03-26` if missing
- Return 400 if unsupported version

### 4. JSON-RPC Methods

| Method | Description | v1 | v2 |
|--------|-------------|----|----|
| `initialize` | Capability exchange | ✅ | ✅ |
| `notifications/initialized` | Client ready signal | ✅ | ✅ |
| `ping` | Health check | ✅ | ✅ |
| `tools/list` | List available tools | ✅ | ✅ |
| `tools/call` | Execute a tool | ✅ | ✅ |
| `resources/list` | List available resources | ✅ | ✅ |
| `resources/read` | Read a resource | ✅ | ✅ |
| `prompts/list` | List prompts | ❌ | ✅ |
| `prompts/get` | Get prompt template | ❌ | ✅ |

### 5. Error Codes

| Code | Meaning |
|------|---------|
| -32700 | Parse error |
| -32600 | Invalid request |
| -32601 | Method not found |
| -32602 | Invalid params |
| -32603 | Internal error |
| -32001 | Authentication error |
| -32002 | Resource not found |

---

## Implementation Phases

### v1 Phases

1. **Project Setup**: Solution structure, package references
2. **Core Types**: Attributes, JSON-RPC models, context classes
3. **Tools**: Registry, POCO binding, schema generation
4. **Resources**: Resource registry, URI template matching
5. **Auth**: `IMcpAuthValidator`, built-in validators
6. **Transport**: Request handler, origin validation, session management
7. **DI Extensions**: `AddMcpServer()`, `AddMcpTools<T>()`, etc.

### v2 Phases

0. **SSE Transport**: Response streaming, GET endpoint
1. **Prompts**: Attributes, registry, handlers
2. **Progress & Cancellation**: Token handling, notifications
3. **Logging**: `IMcpLogger`, log levels
4. **Discovery**: Server cards, OAuth metadata
5. **Fluent API**: Runtime tool definition
6. **Session Store**: State persistence
7. **Middleware**: Pipeline, built-in middleware

---

## Technology Stack

- .NET 8.0+
- Azure Functions (Isolated Worker) 2.0+
- System.Text.Json 8.0
- No external MCP SDK dependency

---

## Quick Start

### 1. Program.cs Setup

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
            mcp.AddResourcesFromType<MyResources>();
            mcp.AddAuthValidator<MyAuthValidator>();
        });
    })
    .Build();
```

### 2. Define Tools

```csharp
public class MyTools
{
    // Simple tool with attribute-based properties
    [McpTool("greet", Description = "Greets a user", AllowAnonymous = true)]
    [McpToolProperty("name", Type = "string", Description = "Name to greet", Required = true)]
    public string Greet(string name) => $"Hello, {name}!";

    // Tool with POCO binding (auto-generates JSON Schema from DataAnnotations)
    [McpTool("create_order", Description = "Creates an order", Title = "Create Order")]
    public object CreateOrder(ToolInvocationContext ctx, CreateOrderRequest request)
    {
        // Access ctx.User for authenticated user
        return new { orderId = Guid.NewGuid(), status = "created" };
    }
}

public class CreateOrderRequest
{
    [Description("Customer ID")]
    [Required]
    public string CustomerId { get; set; } = "";

    [Description("Order items")]
    [Required]
    public List<OrderItem> Items { get; set; } = new();

    [Description("Priority level")]
    [McpAllowedValues("low", "normal", "high")]
    public string Priority { get; set; } = "normal";
}
```

### 3. Define Resources

```csharp
public class MyResources
{
    [McpResource("config://server", "Server Config", AllowAnonymous = true)]
    public McpResourceResult GetConfig(ResourceRequestContext ctx)
    {
        return new McpResourceResult
        {
            Contents = new[] { McpResourceContentItem.FromText(ctx.Uri, "application/json", "{}") }
        };
    }

    [McpResource("items://{category}", "Category Items")]
    public McpResourceResult GetItems(ResourceRequestContext ctx)
    {
        var category = ctx.GetRequiredParameter("category");
        // ...
    }
}
```

### 4. HTTP Endpoint

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

---

## DI Extension Methods

| Method | Description |
|--------|-------------|
| `AddMcp(Action<McpBuilder>)` | Full configuration with builder pattern |
| `AddMcpServer(Action<McpServerOptions>?)` | Quick setup with options |
| `AddMcpTools<T>()` | Register tools from a type |
| `AddMcpResources<T>()` | Register resources from a type |
| `AddMcpAuthValidator<T>()` | Register auth validator |

**McpBuilder methods:**
- `Configure(Action<McpServerOptions>)` - Configure server options
- `AddToolsFromType<T>()` / `AddToolsFromAssembly(Assembly)` - Register tools
- `AddResourcesFromType<T>()` / `AddResourcesFromAssembly(Assembly)` - Register resources
- `AddAuthValidator<T>()` - Register auth validator

---

## Testing

### Manual Testing

```bash
# Initialize
curl -X POST https://localhost:7071/api/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'

# List tools
curl -X POST https://localhost:7071/api/mcp \
  -H "Content-Type: application/json" \
  -H "Mcp-Session-Id: <session-id-from-initialize>" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
```

### MCP Clients

- Claude Desktop
- VS Code MCP extension
- `mcp-remote` adapter for testing

---

## Project Structure

```
src/Muneris.Mcp.AzureFunctions/
├── Attributes/
│   ├── McpToolAttribute.cs
│   ├── McpToolPropertyAttribute.cs
│   ├── McpAllowedValuesAttribute.cs
│   ├── McpResourceAttribute.cs
│   └── McpResourceListAttribute.cs
├── Models/
│   ├── JsonRpcRequest.cs, JsonRpcResponse.cs, JsonRpcError.cs
│   ├── McpToolDefinition.cs, McpToolAnnotations.cs, McpToolResult.cs
│   ├── ToolInvocationContext.cs
│   ├── Content/
│   │   ├── McpContent.cs (abstract)
│   │   ├── McpTextContent.cs, McpImageContent.cs, McpResourceContent.cs
│   └── Resources/
│       ├── McpResourceInfo.cs, McpResourceResult.cs, ResourceRequestContext.cs
├── Services/
│   ├── McpToolRegistry.cs
│   ├── McpResourceRegistry.cs
│   ├── McpRequestHandler.cs
│   └── SchemaGenerator.cs
├── Auth/
│   └── IMcpAuthValidator.cs
└── Extensions/
    ├── McpServerOptions.cs
    ├── McpBuilder.cs
    └── McpServiceCollectionExtensions.cs

samples/Muneris.Mcp.AzureFunctions.Sample/
├── Program.cs
├── McpEndpoint.cs
├── Tools/SampleTools.cs
├── Resources/SampleResources.cs
└── Auth/JwtBearerValidator.cs

tests/Muneris.Mcp.AzureFunctions.Tests/
├── McpToolRegistryTests.cs
├── McpResourceRegistryTests.cs
├── SchemaGeneratorTests.cs
└── JsonRpcModelsTests.cs
```

---

## Notes

- Do NOT use deprecated SSE transport (dual endpoints `/sse` + `/messages`)
- TypeScript SDK's `SSEClientTransport` is deprecated - don't model after it
- v1 focuses on stateless operation; stateful patterns in v2
- Test with real MCP clients to verify compatibility
- POCO binding is a major DX improvement over Microsoft's approach
