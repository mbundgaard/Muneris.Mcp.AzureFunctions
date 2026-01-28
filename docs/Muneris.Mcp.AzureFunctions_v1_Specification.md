# Muneris.Mcp.AzureFunctions v1.0 Specification

## Overview

Version 1.0 delivers a production-ready MCP server for Azure Functions with:

- **Streamable HTTP transport** (MCP spec 2025-11-25, replacing deprecated SSE)
- **Tools** with attribute-based definitions
- **Resources** for exposing read-only data
- **Pluggable authentication** with full HTTP context
- **Server metadata** configuration
- **Origin validation** for DNS rebinding protection

---

## Why This Package Exists

Microsoft's `Microsoft.Azure.Functions.Worker.Extensions.Mcp` has limitations:

| Issue | Microsoft Package | Our Package |
|-------|-------------------|-------------|
| Endpoint | Hardcoded `/runtime/webhooks/mcp` | Configurable (default `/mcp`) |
| Transport | SSE-based (deprecated March 2025) | Streamable HTTP |
| Auth | `webhookAuthorizationLevel` only | Pluggable `IMcpAuthValidator` |
| HTTP Context | Not accessible in handlers | Full access to headers, claims, cookies |
| Resources | Not supported | Full `[McpResource]` support |
| Per-tool auth | Not supported | `AllowAnonymous` per tool/resource |

---

## MCP Specification References

Read these before implementation:

| Document | URL |
|----------|-----|
| Transports (2025-11-25) | https://modelcontextprotocol.io/specification/2025-11-25/basic/transports |
| Authorization | https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization |
| Messages | https://modelcontextprotocol.io/specification/2025-11-25/basic/messages |
| Tools | https://modelcontextprotocol.io/specification/2025-11-25/server/tools |
| Resources | https://modelcontextprotocol.io/specification/2025-11-25/server/resources |
| TypeScript SDK examples | https://github.com/modelcontextprotocol/typescript-sdk/tree/main/examples/server |

---

## v1 Features

### 1. Streamable HTTP Transport

Single endpoint supporting POST and GET. This is the core of the package.

**Endpoint behavior (per MCP spec 2025-11-25):**

| Method | Spec Requirement | v1 Implementation |
|--------|------------------|-------------------|
| POST | Server MUST return `application/json` OR `text/event-stream` | JSON only |
| GET | Server MUST return `text/event-stream` OR `405 Method Not Allowed` | Returns 405 |

**Design choice: JSON-only transport**

v1 deliberately uses JSON-only responses. This is fully spec-compliant (servers choose their response format) and optimized for:

- **Azure Functions Consumption plan** - no long-lived connections
- **Stateless deployments** - each request is independent
- **Simplicity** - no SSE event management, reconnection logic, or stream state

**What you give up:** Progress notifications during execution, server-initiated mid-request messages. These require SSE streaming, available in v2.

**When to use v1:** Request/response tools, simple integrations, serverless deployments, when progress updates aren't critical.

**Required headers:**

| Header | Direction | Purpose |
|--------|-----------|---------|
| `MCP-Protocol-Version` | Request | Protocol version (default `2025-03-26` if missing) |
| `Mcp-Session-Id` | Response (initialize) | Server assigns session ID |
| `Mcp-Session-Id` | Request (subsequent) | Client includes session ID |
| `Origin` | Request | **Must validate** for DNS rebinding protection |
| `Content-Type` | Request | `application/json` |
| `Content-Type` | Response | `application/json` |
| `Accept` | Request | Client sends `application/json, text/event-stream` (we respond with JSON) |

**Origin validation (SECURITY CRITICAL):**

Per MCP spec: "Servers MUST validate the Origin header on all incoming connections to prevent DNS rebinding attacks. If the Origin header is present and invalid, servers MUST respond with HTTP 403 Forbidden."

**Session ID requirements:**

- Should be globally unique and cryptographically secure (UUID, JWT, or hash)
- Must only contain visible ASCII characters (0x21 to 0x7E)
- Server may terminate session at any time → respond 404 to that session ID

---

### 2. JSON-RPC Methods to Implement

#### Core Methods (Required)

| Method | Description | Auth |
|--------|-------------|------|
| `initialize` | Capability exchange, version negotiation | No |
| `notifications/initialized` | Client signals ready (notification, no response) | No |
| `ping` | Health check | No |

#### Tool Methods

| Method | Description | Auth |
|--------|-------------|------|
| `tools/list` | Return available tools with schemas | Configurable |
| `tools/call` | Execute a tool | Per-tool |

#### Resource Methods

| Method | Description | Auth |
|--------|-------------|------|
| `resources/list` | Return available resources | Configurable |
| `resources/read` | Read resource content | Per-resource |

---

### 3. Initialize Handshake

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2025-11-25",
    "capabilities": {
      "tools": {},
      "resources": {}
    },
    "clientInfo": {
      "name": "claude-desktop",
      "version": "1.0.0"
    }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2025-11-25",
    "capabilities": {
      "tools": { "listChanged": false },
      "resources": { "listChanged": false }
    },
    "serverInfo": {
      "name": "My MCP Server",
      "version": "1.0.0"
    },
    "instructions": "Optional instructions for the LLM on how to use this server"
  }
}
```

**Response must include `Mcp-Session-Id` header.**

---

### 4. Tools

Tools are executable functions LLMs can invoke.

#### Tool Definition (for tools/list response)

```json
{
  "name": "get_order",
  "description": "Retrieves order details by ID",
  "inputSchema": {
    "type": "object",
    "properties": {
      "orderId": {
        "type": "string",
        "description": "The order ID to look up"
      }
    },
    "required": ["orderId"]
  },
  "annotations": {
    "title": "Get Order",
    "readOnlyHint": true,
    "destructiveHint": false,
    "idempotentHint": true,
    "openWorldHint": true
  }
}
```

#### Tool Attributes

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class McpToolAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public bool AllowAnonymous { get; set; } = false;
    
    // Annotations (hints for LLM)
    public string? Title { get; set; }
    public bool ReadOnlyHint { get; set; } = false;
    public bool DestructiveHint { get; set; } = false;
    public bool IdempotentHint { get; set; } = false;
    public bool OpenWorldHint { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Parameter)]
public class McpToolPropertyAttribute : Attribute
{
    public string Name { get; }
    public string Type { get; }  // "string", "number", "integer", "boolean", "object", "array"
    public string Description { get; }
    public bool Required { get; set; } = false;
    public string? Default { get; set; }
    public string[]? Enum { get; set; }
    
    // JSON Schema format (for strings)
    public string? Format { get; set; }  // "date", "date-time", "email", "uri", "uuid", "hostname"
    
    // Numeric constraints
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    
    // String constraints
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }  // Regex pattern
}
```

---

### 4a. POCO Binding (Alternative to Individual Parameters)

For tools with multiple parameters, binding to a POCO class significantly reduces boilerplate. The schema is auto-generated from the class properties.

#### Supported Attributes for POCO Properties

Use standard .NET attributes from `System.ComponentModel` and `System.ComponentModel.DataAnnotations`:

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

public class CreateOrderRequest
{
    [Description("The customer's unique identifier")]
    [Required]
    public string CustomerId { get; set; }
    
    [Description("Order line items")]
    [Required]
    public List<OrderItem> Items { get; set; }
    
    [Description("Special instructions or notes")]
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    [Description("Order priority level")]
    [AllowedValues("low", "normal", "high", "urgent")]
    public string Priority { get; set; } = "normal";
    
    [Description("Requested delivery date")]
    [DataType(DataType.Date)]
    public DateTime? DeliveryDate { get; set; }
    
    [Description("Customer email for notifications")]
    [EmailAddress]
    public string? NotificationEmail { get; set; }
    
    [Description("Discount percentage")]
    [Range(0, 100)]
    public decimal? DiscountPercent { get; set; }
}

public class OrderItem
{
    [Description("Menu item ID")]
    [Required]
    public string ItemId { get; set; }
    
    [Description("Quantity to order")]
    [Required]
    [Range(1, 100)]
    public int Quantity { get; set; }
    
    [Description("Special modifications")]
    public string? Modifications { get; set; }
}
```

#### Custom Attribute for Enum Values

Since `[AllowedValues]` is .NET 8+, we provide a fallback:

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class McpAllowedValuesAttribute : Attribute
{
    public object[] Values { get; }
    
    public McpAllowedValuesAttribute(params object[] values)
    {
        Values = values;
    }
}
```

#### POCO Tool Example

```csharp
// Clean: POCO binding
[McpTool("create_order", "Creates a new order")]
public async Task<object> CreateOrder(ToolInvocationContext context, CreateOrderRequest request)
{
    // request is fully populated and validated
    var order = await _orderService.CreateAsync(
        request.CustomerId,
        request.Items,
        request.Notes,
        request.Priority);
        
    return new { orderId = order.Id, status = "created" };
}

// Verbose: Individual parameters (still supported)
[McpTool("create_order", "Creates a new order")]
public async Task<object> CreateOrder(
    ToolInvocationContext context,
    [McpToolProperty("customerId", "string", "Customer ID", Required = true)] string customerId,
    [McpToolProperty("items", "array", "Order items", Required = true)] List<OrderItem> items,
    [McpToolProperty("notes", "string", "Notes", MaxLength = 500)] string? notes,
    [McpToolProperty("priority", "string", "Priority", Enum = new[] { "low", "normal", "high" })] string priority = "normal")
{
    // ...
}
```

#### Generated JSON Schema (from POCO)

The registry auto-generates this schema from `CreateOrderRequest`:

```json
{
  "type": "object",
  "properties": {
    "customerId": {
      "type": "string",
      "description": "The customer's unique identifier"
    },
    "items": {
      "type": "array",
      "description": "Order line items",
      "items": {
        "type": "object",
        "properties": {
          "itemId": {
            "type": "string",
            "description": "Menu item ID"
          },
          "quantity": {
            "type": "integer",
            "description": "Quantity to order",
            "minimum": 1,
            "maximum": 100
          },
          "modifications": {
            "type": "string",
            "description": "Special modifications"
          }
        },
        "required": ["itemId", "quantity"]
      }
    },
    "notes": {
      "type": "string",
      "description": "Special instructions or notes",
      "maxLength": 500
    },
    "priority": {
      "type": "string",
      "description": "Order priority level",
      "enum": ["low", "normal", "high", "urgent"],
      "default": "normal"
    },
    "deliveryDate": {
      "type": "string",
      "description": "Requested delivery date",
      "format": "date"
    },
    "notificationEmail": {
      "type": "string",
      "description": "Customer email for notifications",
      "format": "email"
    },
    "discountPercent": {
      "type": "number",
      "description": "Discount percentage",
      "minimum": 0,
      "maximum": 100
    }
  },
  "required": ["customerId", "items"]
}
```

#### Schema Generation Rules

| .NET Attribute | JSON Schema Output |
|----------------|-------------------|
| `[Description("...")]` | `"description": "..."` |
| `[Required]` | Added to `required` array |
| `[AllowedValues(...)]` or `[McpAllowedValues(...)]` | `"enum": [...]` |
| `[Range(min, max)]` | `"minimum": min, "maximum": max` |
| `[MinLength(n)]` | `"minLength": n` |
| `[MaxLength(n)]` | `"maxLength": n` |
| `[RegularExpression("...")]` | `"pattern": "..."` |
| `[EmailAddress]` | `"format": "email"` |
| `[Url]` | `"format": "uri"` |
| `[Phone]` | `"format": "phone"` (non-standard but useful) |
| `[DataType(DataType.Date)]` | `"format": "date"` |
| `[DataType(DataType.DateTime)]` | `"format": "date-time"` |
| `[DataType(DataType.Time)]` | `"format": "time"` |
| Default value on property | `"default": value` |

#### Type Mapping

| C# Type | JSON Schema Type |
|---------|------------------|
| `string` | `"string"` |
| `int`, `long`, `short` | `"integer"` |
| `float`, `double`, `decimal` | `"number"` |
| `bool` | `"boolean"` |
| `DateTime`, `DateTimeOffset` | `"string"` with format |
| `Guid` | `"string"` with `"format": "uuid"` |
| `List<T>`, `T[]`, `IEnumerable<T>` | `"array"` with `items` |
| Class/record | `"object"` with `properties` |
| `enum` | `"string"` with `enum` values |
| `T?` (nullable) | Not added to `required` |
```

#### Tool Invocation Context

```csharp
public class ToolInvocationContext
{
    public required string ToolName { get; init; }
    public Dictionary<string, object?>? Arguments { get; init; }
    public ClaimsPrincipal? User { get; init; }
    public string? SessionId { get; init; }
    public string? ProtocolVersion { get; init; }
    public required HttpRequestData Request { get; init; }
    public CancellationToken CancellationToken { get; init; }
    
    // Helpers
    public T? GetArgument<T>(string name, T? defaultValue = default);
    public T GetRequiredArgument<T>(string name);
}
```

#### Tool Result

```csharp
public class McpToolResult
{
    public McpContent[] Content { get; set; }
    public bool IsError { get; set; } = false;
}

// Content types
public class McpTextContent : McpContent { public string Text { get; set; } }
public class McpImageContent : McpContent { public string Data { get; set; } public string MimeType { get; set; } }
public class McpResourceContent : McpContent { public string Uri { get; set; } public string MimeType { get; set; } public string? Text { get; set; } }
```

#### Usage Example

```csharp
public class OrderTools
{
    [McpTool("get_order", "Retrieves order details", ReadOnlyHint = true)]
    public async Task<object> GetOrder(
        ToolInvocationContext context,
        [McpToolProperty("orderId", "string", "Order ID", Required = true)] string orderId)
    {
        var order = await _orderService.GetAsync(orderId);
        return new { order.Id, order.Status, order.Total, order.Items };
    }
    
    [McpTool("cancel_order", "Cancels an order", DestructiveHint = true)]
    public async Task<string> CancelOrder(
        ToolInvocationContext context,
        [McpToolProperty("orderId", "string", "Order ID", Required = true)] string orderId,
        [McpToolProperty("reason", "string", "Cancellation reason")] string? reason)
    {
        // context.User available for auth checks
        await _orderService.CancelAsync(orderId, context.User?.FindFirst("sub")?.Value, reason);
        return $"Order {orderId} cancelled";
    }
    
    [McpTool("get_time", "Returns server time", AllowAnonymous = true)]
    public string GetTime(ToolInvocationContext context)
    {
        return DateTime.UtcNow.ToString("O");
    }
}
```

---

### 5. Resources

Resources expose read-only data. Different from tools: resources are for reading context, tools are for actions.

#### Resource Definition (for resources/list response)

```json
{
  "uri": "menu://categories/appetizers",
  "name": "Appetizers Menu",
  "description": "All appetizer items",
  "mimeType": "application/json"
}
```

#### Resource Attributes

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class McpResourceAttribute : Attribute
{
    public string UriPattern { get; }  // "menu://items/{category}" with params
    public string Name { get; }
    public string Description { get; }
    public string MimeType { get; set; } = "application/json";
    public bool AllowAnonymous { get; set; } = false;
}

// For dynamic resource listing
[AttributeUsage(AttributeTargets.Method)]
public class McpResourceListAttribute : Attribute
{
    public string Scheme { get; }  // "menu", "config", etc.
}
```

#### Resource Context

```csharp
public class ResourceRequestContext
{
    public required string Uri { get; init; }
    public Dictionary<string, string> Parameters { get; init; }  // From URI pattern
    public ClaimsPrincipal? User { get; init; }
    public required HttpRequestData Request { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
```

#### Resource Result

```csharp
public class McpResourceResult
{
    public McpResourceContent[] Contents { get; set; }
}

public class McpResourceContent
{
    public required string Uri { get; set; }
    public required string MimeType { get; set; }
    public string? Text { get; set; }  // For text content
    public string? Blob { get; set; }  // Base64 for binary
}
```

#### Usage Example

```csharp
public class MenuResources
{
    // List available resources dynamically
    [McpResourceList("menu")]
    public async Task<IEnumerable<McpResourceInfo>> ListMenuResources(ResourceRequestContext context)
    {
        var categories = await _menuService.GetCategoriesAsync();
        return categories.Select(c => new McpResourceInfo
        {
            Uri = $"menu://categories/{c.Id}",
            Name = c.Name,
            Description = $"Menu items in {c.Name}",
            MimeType = "application/json"
        });
    }
    
    // Read a specific resource
    [McpResource("menu://categories/{categoryId}", "Menu Category", "Items in category")]
    public async Task<McpResourceResult> GetCategory(ResourceRequestContext context)
    {
        var items = await _menuService.GetItemsAsync(context.Parameters["categoryId"]);
        return new McpResourceResult
        {
            Contents = new[] { new McpResourceContent
            {
                Uri = context.Uri,
                MimeType = "application/json",
                Text = JsonSerializer.Serialize(items)
            }}
        };
    }
    
    // Static resource
    [McpResource("config://settings", "Server Config", "Configuration", AllowAnonymous = true)]
    public McpResourceResult GetConfig(ResourceRequestContext context)
    {
        return new McpResourceResult { /* ... */ };
    }
}
```

---

### 6. Authentication

MCP server acts as an **OAuth 2.1 Resource Server** - validates tokens, doesn't issue them.

#### Auth Interface

```csharp
public interface IMcpAuthValidator
{
    Task<ClaimsPrincipal?> ValidateRequestAsync(HttpRequestData request, CancellationToken ct = default);
    string GetAuthError(HttpRequestData request) => "Unauthorized";
    string? GetWwwAuthenticateHeader(HttpRequestData request) => null;
}
```

#### Auth Behavior

1. If no `IMcpAuthValidator` registered → all endpoints open
2. If validator registered:
   - `[McpTool(..., AllowAnonymous = true)]` → skip auth
   - Protected tool/resource + auth fails → HTTP 401 with `WWW-Authenticate` header
   - Protected tool/resource + auth succeeds → `context.User` populated

#### WWW-Authenticate Header (per MCP spec)

When returning 401, include header pointing to OAuth metadata:

```
WWW-Authenticate: Bearer realm="mcp", resource_metadata="/.well-known/oauth-protected-resource"
```

---

### 7. Server Configuration

```csharp
public class McpServerOptions
{
    public string ServerName { get; set; } = "MCP Server";
    public string ServerVersion { get; set; } = "1.0.0";
    public string? Instructions { get; set; }  // Guidance for LLM
    
    // Security
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();  // Empty = allow all
    public bool RequireOriginHeader { get; set; } = false;
    
    // Protocol
    public string[] SupportedProtocolVersions { get; set; } = new[] { "2025-11-25", "2025-03-26" };
    
    // Features
    public bool EnableTools { get; set; } = true;
    public bool EnableResources { get; set; } = true;
}
```

---

### 8. Dependency Injection Setup

```csharp
public static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddMcpServer(
        this IServiceCollection services, 
        Action<McpServerOptions>? configure = null);
    
    public static IServiceCollection AddMcpAuthValidator<T>(
        this IServiceCollection services) where T : class, IMcpAuthValidator;
    
    public static IServiceCollection AddMcpTools<T>(
        this IServiceCollection services) where T : class;
    
    public static IServiceCollection AddMcpResources<T>(
        this IServiceCollection services) where T : class;
}
```

#### Registration Example

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddMcpServer(options =>
        {
            options.ServerName = "Muneris POS Server";
            options.ServerVersion = "1.0.0";
            options.Instructions = "This server provides POS and menu management tools.";
            options.AllowedOrigins = new[] { "https://claude.ai", "https://myapp.com" };
        });
        
        services.AddMcpAuthValidator<JwtBearerValidator>();
        services.AddMcpTools<OrderTools>();
        services.AddMcpTools<MenuTools>();
        services.AddMcpResources<MenuResources>();
    })
    .Build();
```

---

### 9. Azure Function Endpoint

```csharp
public class McpEndpoint
{
    private readonly McpRequestHandler _handler;
    
    public McpEndpoint(McpRequestHandler handler) => _handler = handler;
    
    [Function("Mcp")]
    public Task<HttpResponseData> HandleMcp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mcp")]
        HttpRequestData request)
    {
        return _handler.HandleAsync(request);
    }
    
    // Optional: Explicitly return 405 for GET requests
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
```

---

### 10. Error Handling

#### JSON-RPC Error Codes

| Code | Constant | Meaning |
|------|----------|---------|
| -32700 | ParseError | Invalid JSON |
| -32600 | InvalidRequest | Invalid JSON-RPC request |
| -32601 | MethodNotFound | Method not found |
| -32602 | InvalidParams | Invalid method parameters |
| -32603 | InternalError | Internal server error |
| -32001 | AuthenticationError | Authentication failed (custom) |
| -32002 | ResourceNotFound | Resource not found (custom) |

#### Error Response Format

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32601,
    "message": "Method not found",
    "data": { "method": "unknown/method" }
  }
}
```

---

## Project Structure

```
Muneris.Mcp.AzureFunctions/
├── src/
│   └── Muneris.Mcp.AzureFunctions/
│       ├── Attributes/
│       │   ├── McpToolAttribute.cs
│       │   ├── McpToolPropertyAttribute.cs
│       │   ├── McpAllowedValuesAttribute.cs
│       │   ├── McpResourceAttribute.cs
│       │   └── McpResourceListAttribute.cs
│       ├── Models/
│       │   ├── JsonRpc/
│       │   │   ├── JsonRpcRequest.cs
│       │   │   ├── JsonRpcResponse.cs
│       │   │   └── JsonRpcError.cs
│       │   ├── Tools/
│       │   │   ├── McpToolDefinition.cs
│       │   │   ├── McpToolResult.cs
│       │   │   └── ToolInvocationContext.cs
│       │   ├── Resources/
│       │   │   ├── McpResourceInfo.cs
│       │   │   ├── McpResourceResult.cs
│       │   │   └── ResourceRequestContext.cs
│       │   ├── Content/
│       │   │   ├── McpContent.cs
│       │   │   ├── McpTextContent.cs
│       │   │   ├── McpImageContent.cs
│       │   │   └── McpResourceContent.cs
│       │   └── Protocol/
│       │       ├── InitializeParams.cs
│       │       ├── InitializeResult.cs
│       │       ├── ServerCapabilities.cs
│       │       └── ClientCapabilities.cs
│       ├── Auth/
│       │   ├── IMcpAuthValidator.cs
│       │   ├── NoAuthValidator.cs
│       │   └── ApiKeyValidator.cs
│       ├── Services/
│       │   ├── McpToolRegistry.cs
│       │   ├── McpResourceRegistry.cs
│       │   └── McpRequestHandler.cs
│       ├── Extensions/
│       │   └── McpServiceCollectionExtensions.cs
│       ├── McpServerOptions.cs
│       └── Muneris.Mcp.AzureFunctions.csproj
├── samples/
│   └── SampleMcpServer/
├── tests/
│   └── Muneris.Mcp.AzureFunctions.Tests/
└── Muneris.Mcp.AzureFunctions.sln
```

---

## Testing

### Manual Testing with curl

```bash
# Initialize
curl -X POST http://localhost:7071/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'

# List tools
curl -X POST http://localhost:7071/mcp \
  -H "Content-Type: application/json" \
  -H "Mcp-Session-Id: <session-id-from-init>" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'

# Call tool
curl -X POST http://localhost:7071/mcp \
  -H "Content-Type: application/json" \
  -H "Mcp-Session-Id: <session-id>" \
  -H "Authorization: Bearer <token>" \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_order","arguments":{"orderId":"123"}}}'

# List resources
curl -X POST http://localhost:7071/mcp \
  -H "Content-Type: application/json" \
  -H "Mcp-Session-Id: <session-id>" \
  -d '{"jsonrpc":"2.0","id":4,"method":"resources/list"}'

# Read resource
curl -X POST http://localhost:7071/mcp \
  -H "Content-Type: application/json" \
  -H "Mcp-Session-Id: <session-id>" \
  -d '{"jsonrpc":"2.0","id":5,"method":"resources/read","params":{"uri":"menu://categories/appetizers"}}'
```

### Test with MCP Clients

- Claude Desktop
- VS Code with MCP extension
- mcp-remote adapter

---

## Implementation Checklist

### Phase 1: Core Infrastructure
- [ ] Solution and project structure
- [ ] JSON-RPC models
- [ ] McpServerOptions
- [ ] McpRequestHandler (routing, Origin validation, session handling)
- [ ] DI extensions

### Phase 2: Tools
- [ ] McpToolAttribute, McpToolPropertyAttribute
- [ ] McpAllowedValuesAttribute (for pre-.NET 8 compatibility)
- [ ] McpToolRegistry (reflection-based discovery)
- [ ] POCO binding support with schema generation
- [ ] DataAnnotations attribute scanning ([Description], [Required], [Range], etc.)
- [ ] Nested object schema generation (single level)
- [ ] C# type to JSON Schema type mapping
- [ ] ToolInvocationContext
- [ ] Tool result types
- [ ] tools/list handler
- [ ] tools/call handler

### Phase 3: Resources
- [ ] McpResourceAttribute, McpResourceListAttribute
- [ ] McpResourceRegistry
- [ ] ResourceRequestContext
- [ ] Resource result types
- [ ] resources/list handler
- [ ] resources/read handler

### Phase 4: Authentication
- [ ] IMcpAuthValidator interface
- [ ] NoAuthValidator, ApiKeyValidator built-ins
- [ ] Per-tool/resource AllowAnonymous handling
- [ ] WWW-Authenticate header support
- [ ] 401 response handling

### Phase 5: Polish
- [ ] XML documentation on all public types
- [ ] Sample project with realistic examples
- [ ] Unit tests
- [ ] README with quick start guide

---

## Package Dependencies

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.0.0" />
  <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" Version="3.2.0" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
  <PackageReference Include="System.Text.Json" Version="8.0.0" />
</ItemGroup>
```

No external MCP SDK dependency - implement protocol directly for full control.
