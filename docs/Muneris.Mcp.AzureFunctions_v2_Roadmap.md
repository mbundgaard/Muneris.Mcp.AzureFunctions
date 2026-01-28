# Muneris.Mcp.AzureFunctions v2.0 Roadmap

## Overview

v2 extends the package in two dimensions:

**Transport:** Full SSE streaming support within Streamable HTTP

**Features:** Prompts, progress, cancellation, logging, subscriptions, discovery endpoints, and more

---

## Transport Modes

### JSON-Only Mode (v1 default)

Server responds with `application/json` to POST, returns 405 for GET.

Best for: Azure Consumption plan, stateless deployments, simple request/response tools.

### SSE Streaming Mode (v2)

Server can respond with `text/event-stream` to POST when streaming is beneficial. GET endpoint returns SSE stream for server-initiated messages.

Best for: Long-running operations, progress updates, real-time notifications, Premium/Dedicated plans.

#### POST Response Options

Per spec, server chooses response format per-request:

**JSON Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"Done"}]}}
```

**SSE Response (for streaming scenarios):**
```http
HTTP/1.1 200 OK
Content-Type: text/event-stream

id: evt-001
data: 

id: evt-002
data: {"jsonrpc":"2.0","method":"notifications/progress","params":{"progressToken":"abc","progress":50,"total":100}}

id: evt-003
data: {"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"Done"}]}}
```

#### When Server Uses SSE Response

- Tool request includes `_meta.progressToken`
- Operation will send notifications before completing
- Long-running request benefits from incremental feedback

Otherwise, server returns JSON (simpler, lower overhead).

#### GET Endpoint for Server-Initiated Messages

Per spec: "The client MAY issue an HTTP GET to the MCP endpoint. This can be used to open an SSE stream, allowing the server to communicate to the client, without the client first sending data via HTTP POST."

```http
GET /mcp HTTP/1.1
Accept: text/event-stream
Mcp-Session-Id: session-123

HTTP/1.1 200 OK
Content-Type: text/event-stream

id: evt-100
data: {"jsonrpc":"2.0","method":"notifications/resources/updated","params":{"uri":"menu://items/123"}}
```

Used for:
- Resource change notifications (when subscribed)
- Server-initiated requests (sampling, elicitation)

#### Resumability via Last-Event-ID

Per spec, clients can resume after disconnection:

```http
GET /mcp HTTP/1.1
Accept: text/event-stream
Mcp-Session-Id: session-123
Last-Event-ID: evt-002
```

Server replays missed events from that stream. Requires session storage.

#### Transport Configuration

```csharp
public class McpServerOptions
{
    // Transport mode
    public bool EnableSseResponses { get; set; } = true;
    public bool EnableGetSseStream { get; set; } = true;
    public bool EnableResumability { get; set; } = false;  // Requires session storage
    public TimeSpan SseRetryInterval { get; set; } = TimeSpan.FromSeconds(3);
}
```

#### Azure Functions Deployment Considerations

| Plan | SSE Support | Recommendation |
|------|-------------|----------------|
| Consumption | Limited (230s timeout) | Use JSON-only mode |
| Premium | Good | SSE works well |
| Dedicated (App Service) | Full | SSE works well |
| Container Apps | Full | SSE works well |

---

## Spec Compliance: v1 vs v2

| Capability | Spec Requirement | v1 | v2 |
|------------|------------------|----|----|
| POST → JSON response | Server may choose | ✅ Always | ✅ Default |
| POST → SSE response | Server may choose | ❌ | ✅ When beneficial |
| GET → SSE stream | Server must return SSE or 405 | Returns 405 | ✅ SSE stream |
| Resumability | Server may implement | ❌ | ✅ Optional |

Both versions are fully spec-compliant. v2 exercises more of the spec's optional capabilities.

---

## MCP Specification References

| Feature | Spec URL |
|---------|----------|
| Prompts | https://modelcontextprotocol.io/specification/2025-11-25/server/prompts |
| Progress | https://modelcontextprotocol.io/specification/2025-11-25/utilities/progress |
| Cancellation | https://modelcontextprotocol.io/specification/2025-11-25/utilities/cancellation |
| Logging | https://modelcontextprotocol.io/specification/2025-11-25/utilities/logging |
| Elicitation | https://modelcontextprotocol.io/specification/2025-11-25/server/elicitation |
| Sampling | https://modelcontextprotocol.io/specification/2025-11-25/client/sampling |

---

## v2 Features

### 1. Prompts

Prompts are predefined templates that guide LLM interactions. Unlike tools (which perform actions), prompts provide structured instructions and context.

#### Use Cases

- Code review templates
- Document analysis workflows
- Multi-step reasoning patterns
- Standardized report formats

#### Prompt Definition (for prompts/list response)

```json
{
  "name": "analyze_order_issue",
  "title": "Analyze Order Issue",
  "description": "Analyze customer order issues and suggest resolutions",
  "arguments": [
    {
      "name": "orderId",
      "description": "The order ID to analyze",
      "required": true
    },
    {
      "name": "issueType",
      "description": "Type of issue: missing_item, wrong_item, late_delivery, quality",
      "required": true
    }
  ]
}
```

#### Prompt Get Response

```json
{
  "description": "Order issue analysis prompt",
  "messages": [
    {
      "role": "user",
      "content": {
        "type": "text",
        "text": "Analyze order #12345 for a missing_item issue. Review the order details and suggest appropriate resolution steps."
      }
    },
    {
      "role": "user", 
      "content": {
        "type": "resource",
        "resource": {
          "uri": "order://12345",
          "mimeType": "application/json",
          "text": "{\"id\":\"12345\",\"items\":[...],\"status\":\"delivered\"}"
        }
      }
    }
  ]
}
```

#### Prompt Attributes

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class McpPromptAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public string? Title { get; set; }
    public bool AllowAnonymous { get; set; } = false;
}

[AttributeUsage(AttributeTargets.Parameter)]
public class McpPromptArgumentAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public bool Required { get; set; } = false;
}
```

#### Prompt Context

```csharp
public class PromptRequestContext
{
    public required string PromptName { get; init; }
    public Dictionary<string, string>? Arguments { get; init; }
    public ClaimsPrincipal? User { get; init; }
    public required HttpRequestData Request { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
```

#### Prompt Result

```csharp
public class McpPromptResult
{
    public string? Description { get; set; }
    public McpPromptMessage[] Messages { get; set; }
}

public class McpPromptMessage
{
    public string Role { get; set; }  // "user" or "assistant"
    public McpContent Content { get; set; }
}
```

#### Usage Example

```csharp
public class SupportPrompts
{
    private readonly IOrderService _orderService;
    
    [McpPrompt("analyze_order_issue", "Analyze order issues and suggest resolutions")]
    public async Task<McpPromptResult> AnalyzeOrderIssue(
        PromptRequestContext context,
        [McpPromptArgument("orderId", "Order ID", Required = true)] string orderId,
        [McpPromptArgument("issueType", "Issue type")] string? issueType)
    {
        var order = await _orderService.GetAsync(orderId);
        
        return new McpPromptResult
        {
            Description = "Order issue analysis",
            Messages = new[]
            {
                new McpPromptMessage
                {
                    Role = "user",
                    Content = new McpTextContent
                    {
                        Text = $"Analyze order #{orderId} for a {issueType ?? "general"} issue. " +
                               "Review the order details below and suggest resolution steps."
                    }
                },
                new McpPromptMessage
                {
                    Role = "user",
                    Content = new McpResourceContent
                    {
                        Uri = $"order://{orderId}",
                        MimeType = "application/json",
                        Text = JsonSerializer.Serialize(order)
                    }
                }
            }
        };
    }
}
```

#### JSON-RPC Methods

| Method | Description |
|--------|-------------|
| `prompts/list` | List available prompts |
| `prompts/get` | Get prompt messages with arguments filled in |

---

### 2. Progress Notifications

Report progress during long-running tool calls. Essential for operations like data sync, report generation, bulk imports.

**SSE within Streamable HTTP (per MCP spec 2025-11-25):**

The MCP spec allows servers to return either `application/json` or `text/event-stream` for POST responses. v1 implements JSON-only; v2 adds SSE streaming support for:

- Progress updates during long operations
- Server-initiated notifications related to the request
- Multiple messages before the final response

This is NOT the deprecated "HTTP+SSE" dual-endpoint transport - it's SSE as a response format within the single `/mcp` endpoint.

**GET endpoint:** v2 also implements the optional GET endpoint returning `text/event-stream` for server-to-client notifications unrelated to active requests (resource change notifications, etc.).

#### Protocol

Server sends progress via SSE stream.

```json
{
  "jsonrpc": "2.0",
  "method": "notifications/progress",
  "params": {
    "progressToken": "token-123",
    "progress": 50,
    "total": 100,
    "message": "Processing item 50 of 100"
  }
}
```

#### Implementation

```csharp
public class ToolInvocationContext
{
    // ... existing properties ...
    
    /// <summary>
    /// Progress token from the request. Null if client didn't request progress.
    /// </summary>
    public string? ProgressToken { get; init; }
    
    /// <summary>
    /// Reports progress to the client.
    /// </summary>
    public Func<int, int?, string?, Task>? ReportProgress { get; init; }
}
```

#### Usage Example

```csharp
[McpTool("sync_orders", "Synchronize orders from external system")]
public async Task<string> SyncOrders(ToolInvocationContext context)
{
    var orders = await _externalService.GetPendingOrdersAsync();
    var total = orders.Count;
    var processed = 0;
    
    foreach (var order in orders)
    {
        await _orderService.ImportAsync(order);
        processed++;
        
        // Report progress if client requested it
        if (context.ReportProgress != null)
        {
            await context.ReportProgress(processed, total, $"Imported order {order.Id}");
        }
    }
    
    return $"Synchronized {processed} orders";
}
```

#### Client Request with Progress Token

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "sync_orders",
    "arguments": {},
    "_meta": {
      "progressToken": "progress-abc123"
    }
  }
}
```

---

### 3. Cancellation

Allow clients to cancel in-flight requests.

#### Protocol

Client sends cancellation notification:

```json
{
  "jsonrpc": "2.0",
  "method": "notifications/cancelled",
  "params": {
    "requestId": 1,
    "reason": "User requested cancellation"
  }
}
```

#### Implementation

The `CancellationToken` in `ToolInvocationContext` is already wired up. For v2:

```csharp
public class McpRequestHandler
{
    private readonly ConcurrentDictionary<object, CancellationTokenSource> _pendingRequests = new();
    
    // When receiving notifications/cancelled, cancel the matching request
    private void HandleCancellation(object requestId, string? reason)
    {
        if (_pendingRequests.TryRemove(requestId, out var cts))
        {
            cts.Cancel();
        }
    }
}
```

#### Usage

Tool implementations should respect the cancellation token:

```csharp
[McpTool("long_operation", "A long-running operation")]
public async Task<string> LongOperation(ToolInvocationContext context)
{
    for (int i = 0; i < 100; i++)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(100, context.CancellationToken);
    }
    return "Completed";
}
```

---

### 4. Logging

Send structured log messages from server to client for debugging.

#### Protocol

Server sends log notifications:

```json
{
  "jsonrpc": "2.0",
  "method": "notifications/message",
  "params": {
    "level": "info",
    "logger": "OrderTools",
    "data": "Processing order 12345"
  }
}
```

#### Log Levels

| Level | Use |
|-------|-----|
| `debug` | Detailed debugging information |
| `info` | General operational information |
| `notice` | Normal but significant events |
| `warning` | Warning conditions |
| `error` | Error conditions |
| `critical` | Critical conditions |
| `alert` | Action must be taken immediately |
| `emergency` | System is unusable |

#### Implementation

```csharp
public interface IMcpLogger
{
    Task LogAsync(string level, string logger, object data);
}

public class ToolInvocationContext
{
    // ... existing properties ...
    
    /// <summary>
    /// Logger for sending messages to MCP client.
    /// </summary>
    public IMcpLogger? McpLogger { get; init; }
}
```

#### Usage Example

```csharp
[McpTool("import_menu", "Import menu from external system")]
public async Task<string> ImportMenu(ToolInvocationContext context)
{
    await context.McpLogger?.LogAsync("info", "MenuTools", "Starting menu import");
    
    try
    {
        var items = await _externalService.GetMenuItemsAsync();
        await context.McpLogger?.LogAsync("debug", "MenuTools", $"Found {items.Count} items");
        
        foreach (var item in items)
        {
            await _menuService.UpsertAsync(item);
        }
        
        await context.McpLogger?.LogAsync("info", "MenuTools", "Menu import completed");
        return $"Imported {items.Count} items";
    }
    catch (Exception ex)
    {
        await context.McpLogger?.LogAsync("error", "MenuTools", new { message = ex.Message, stack = ex.StackTrace });
        throw;
    }
}
```

#### Server Capability

Declare logging capability in initialize response:

```json
{
  "capabilities": {
    "logging": {}
  }
}
```

---

### 5. Resource Subscriptions

Allow clients to subscribe to resource changes and receive notifications.

#### Protocol

Subscribe request:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "resources/subscribe",
  "params": {
    "uri": "menu://categories/appetizers"
  }
}
```

Change notification:
```json
{
  "jsonrpc": "2.0",
  "method": "notifications/resources/updated",
  "params": {
    "uri": "menu://categories/appetizers"
  }
}
```

#### Implementation

```csharp
public interface IMcpResourceNotifier
{
    Task NotifyResourceUpdatedAsync(string uri);
    Task NotifyResourceListChangedAsync();
}

// In your service layer:
public class MenuService
{
    private readonly IMcpResourceNotifier _notifier;
    
    public async Task UpdateItemAsync(MenuItem item)
    {
        await _repository.UpdateAsync(item);
        await _notifier.NotifyResourceUpdatedAsync($"menu://items/{item.Id}");
    }
}
```

#### Server Capability

```json
{
  "capabilities": {
    "resources": {
      "subscribe": true,
      "listChanged": true
    }
  }
}
```

---

### 6. Server Cards (.well-known/mcp.json)

Allow capability discovery without connecting. Useful for server registries and client UIs.

#### Endpoint

`GET /.well-known/mcp.json`

#### Response

```json
{
  "name": "Muneris POS Server",
  "version": "1.0.0",
  "description": "POS and menu management for hospitality",
  "homepage": "https://muneris.dk",
  "icon": "https://muneris.dk/icon.png",
  "capabilities": {
    "tools": true,
    "resources": true,
    "prompts": true
  },
  "authentication": {
    "type": "oauth2",
    "metadata": "/.well-known/oauth-protected-resource"
  },
  "endpoints": {
    "mcp": "/mcp"
  }
}
```

#### Implementation

Add a separate Azure Function or include in the MCP handler:

```csharp
[Function("McpServerCard")]
public HttpResponseData GetServerCard(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/mcp.json")]
    HttpRequestData request)
{
    var response = request.CreateResponse(HttpStatusCode.OK);
    response.Headers.Add("Content-Type", "application/json");
    response.WriteString(JsonSerializer.Serialize(_serverCard));
    return response;
}
```

---

### 7. OAuth Protected Resource Metadata

Per MCP authorization spec, servers should expose OAuth metadata.

#### Endpoint

`GET /.well-known/oauth-protected-resource`

#### Response (RFC 9728)

```json
{
  "resource": "https://myserver.com/mcp",
  "authorization_servers": ["https://auth.mycompany.com"],
  "bearer_methods_supported": ["header"],
  "scopes_supported": ["mcp:tools", "mcp:resources", "mcp:prompts"],
  "resource_documentation": "https://docs.myserver.com/mcp"
}
```

#### Implementation

```csharp
[Function("OAuthProtectedResource")]
public HttpResponseData GetProtectedResourceMetadata(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", 
        Route = ".well-known/oauth-protected-resource")]
    HttpRequestData request)
{
    var metadata = new
    {
        resource = $"{request.Url.Scheme}://{request.Url.Host}/mcp",
        authorization_servers = _options.AuthorizationServers,
        bearer_methods_supported = new[] { "header" },
        scopes_supported = _options.ScopesSupported
    };
    
    var response = request.CreateResponse(HttpStatusCode.OK);
    response.Headers.Add("Content-Type", "application/json");
    response.WriteString(JsonSerializer.Serialize(metadata));
    return response;
}
```

---

### 8. Elicitation

Server requests additional information from user during tool execution.

#### Use Case

Tool needs clarification or confirmation before proceeding.

#### Protocol

Server sends elicitation request:
```json
{
  "jsonrpc": "2.0",
  "id": "elicit-1",
  "method": "elicitation/request",
  "params": {
    "message": "Multiple orders found. Which one do you mean?",
    "requestedSchema": {
      "type": "object",
      "properties": {
        "selection": {
          "type": "string",
          "enum": ["Order #123 - $45.00", "Order #124 - $67.50", "Order #125 - $23.00"]
        }
      },
      "required": ["selection"]
    }
  }
}
```

Client responds:
```json
{
  "jsonrpc": "2.0",
  "id": "elicit-1",
  "result": {
    "action": "accept",
    "content": {
      "selection": "Order #124 - $67.50"
    }
  }
}
```

#### Implementation

```csharp
public class ToolInvocationContext
{
    // ... existing properties ...
    
    /// <summary>
    /// Request additional information from the user.
    /// Returns null if user declined or timed out.
    /// </summary>
    public Func<string, JsonObject, Task<JsonObject?>>? ElicitAsync { get; init; }
}
```

#### Usage Example

```csharp
[McpTool("refund_order", "Process a refund for an order")]
public async Task<string> RefundOrder(
    ToolInvocationContext context,
    [McpToolProperty("customerName", "string", "Customer name", Required = true)] string customerName)
{
    var orders = await _orderService.GetByCustomerAsync(customerName);
    
    if (orders.Count > 1 && context.ElicitAsync != null)
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["orderId"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = JsonSerializer.SerializeToNode(
                        orders.Select(o => $"{o.Id} - {o.Total:C}").ToArray())
                }
            },
            ["required"] = new JsonArray { "orderId" }
        };
        
        var response = await context.ElicitAsync(
            "Multiple orders found. Which one should be refunded?", 
            schema);
        
        if (response == null)
            return "Refund cancelled by user";
            
        var selectedId = response["orderId"]?.GetValue<string>();
        // Process refund for selected order...
    }
    
    // Single order case...
}
```

#### Server Capability

```json
{
  "capabilities": {
    "elicitation": {}
  }
}
```

---

### 9. Batch Requests

Process multiple JSON-RPC requests in a single HTTP request.

#### Request Format

```json
[
  {"jsonrpc": "2.0", "id": 1, "method": "tools/list"},
  {"jsonrpc": "2.0", "id": 2, "method": "resources/list"},
  {"jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": {"name": "get_time"}}
]
```

#### Response Format

```json
[
  {"jsonrpc": "2.0", "id": 1, "result": {"tools": [...]}},
  {"jsonrpc": "2.0", "id": 2, "result": {"resources": [...]}},
  {"jsonrpc": "2.0", "id": 3, "result": {"content": [...]}}
]
```

#### Implementation Considerations

- Process requests in parallel where possible
- Maintain order in response array
- Handle partial failures (some succeed, some fail)
- Respect rate limits across batch

---

### 10. Fluent Tool Definition

Alternative to attributes for runtime-generated tools, external tool definitions, or dynamic scenarios.

#### Use Cases

- Tools loaded from configuration/database
- Multi-tenant scenarios with tenant-specific tools
- Plugin systems where tools are discovered at runtime
- Testing/mocking scenarios

#### Builder API

```csharp
public interface IMcpToolBuilder
{
    IMcpToolBuilder WithDescription(string description);
    IMcpToolBuilder WithTitle(string title);
    IMcpToolBuilder AllowAnonymous(bool allow = true);
    IMcpToolBuilder WithAnnotations(Action<ToolAnnotationsBuilder> configure);
    IMcpToolBuilder WithProperty(string name, string type, string description, 
        bool required = false, Action<PropertyBuilder>? configure = null);
    IMcpToolBuilder WithInputSchema(JsonObject schema);  // Full schema override
    IMcpToolBuilder HandledBy<THandler>() where THandler : IMcpToolHandler;
    IMcpToolBuilder HandledBy(Func<ToolInvocationContext, Task<object?>> handler);
    IMcpToolBuilder HandledBy(Func<ToolInvocationContext, Dictionary<string, object?>, Task<object?>> handler);
}

public interface IMcpToolHandler
{
    Task<object?> HandleAsync(ToolInvocationContext context, CancellationToken ct = default);
}

public class PropertyBuilder
{
    public PropertyBuilder WithDefault(object value);
    public PropertyBuilder WithEnum(params object[] values);
    public PropertyBuilder WithFormat(string format);
    public PropertyBuilder WithRange(double? min, double? max);
    public PropertyBuilder WithLength(int? min, int? max);
    public PropertyBuilder WithPattern(string regex);
}
```

#### Registration Examples

```csharp
services.AddMcpServer(mcp =>
{
    // Simple inline handler
    mcp.DefineTool("get_server_time")
       .WithDescription("Returns the current server time")
       .AllowAnonymous()
       .HandledBy(async context => DateTime.UtcNow.ToString("O"));
    
    // With parameters
    mcp.DefineTool("search_orders")
       .WithDescription("Search orders by criteria")
       .WithProperty("customerId", "string", "Customer ID filter")
       .WithProperty("status", "string", "Order status", configure: p => 
           p.WithEnum("pending", "confirmed", "completed", "cancelled"))
       .WithProperty("fromDate", "string", "Start date", configure: p => 
           p.WithFormat("date"))
       .WithProperty("limit", "integer", "Max results", configure: p => 
           p.WithDefault(10).WithRange(1, 100))
       .HandledBy<OrderSearchHandler>();
    
    // Full schema override (for complex schemas)
    mcp.DefineTool("advanced_query")
       .WithDescription("Execute advanced query")
       .WithInputSchema(JsonNode.Parse("""
       {
           "type": "object",
           "properties": {
               "query": { "$ref": "#/definitions/QueryExpression" }
           },
           "definitions": {
               "QueryExpression": { ... }
           }
       }
       """).AsObject())
       .HandledBy<AdvancedQueryHandler>();
    
    // Handler with typed arguments
    mcp.DefineTool("calculate")
       .WithDescription("Perform calculation")
       .WithProperty("a", "number", "First operand", required: true)
       .WithProperty("b", "number", "Second operand", required: true)
       .WithProperty("operation", "string", "Operation", required: true,
           configure: p => p.WithEnum("add", "subtract", "multiply", "divide"))
       .HandledBy(async (context, args) =>
       {
           var a = Convert.ToDouble(args["a"]);
           var b = Convert.ToDouble(args["b"]);
           var op = args["operation"]?.ToString();
           
           return op switch
           {
               "add" => a + b,
               "subtract" => a - b,
               "multiply" => a * b,
               "divide" => b != 0 ? a / b : double.NaN,
               _ => throw new ArgumentException($"Unknown operation: {op}")
           };
       });
});
```

#### Handler Class Example

```csharp
public class OrderSearchHandler : IMcpToolHandler
{
    private readonly IOrderService _orderService;
    
    public OrderSearchHandler(IOrderService orderService)
    {
        _orderService = orderService;
    }
    
    public async Task<object?> HandleAsync(ToolInvocationContext context, CancellationToken ct)
    {
        var customerId = context.GetArgument<string>("customerId");
        var status = context.GetArgument<string>("status");
        var fromDate = context.GetArgument<DateTime?>("fromDate");
        var limit = context.GetArgument<int>("limit", 10);
        
        var orders = await _orderService.SearchAsync(customerId, status, fromDate, limit, ct);
        return orders;
    }
}
```

#### Dynamic Tool Registration

```csharp
// Load tools from database at startup
public class DynamicToolLoader : IHostedService
{
    private readonly IMcpToolRegistry _registry;
    private readonly IToolDefinitionStore _store;
    
    public async Task StartAsync(CancellationToken ct)
    {
        var definitions = await _store.GetAllAsync(ct);
        
        foreach (var def in definitions)
        {
            _registry.RegisterTool(def.Name, builder =>
            {
                builder.WithDescription(def.Description);
                
                foreach (var prop in def.Properties)
                {
                    builder.WithProperty(prop.Name, prop.Type, prop.Description, 
                        prop.Required);
                }
                
                builder.HandledBy(async context =>
                {
                    // Dynamic execution logic
                    return await ExecuteDynamicTool(def, context);
                });
            });
        }
    }
    
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

---

### 11. Session State Store

Persistent session state for multi-request workflows, wizard patterns, and state accumulation.

#### Interface

```csharp
public interface IMcpSessionStore
{
    /// <summary>Get a value from session state.</summary>
    Task<T?> GetAsync<T>(string sessionId, string key, CancellationToken ct = default);
    
    /// <summary>Set a value in session state.</summary>
    Task SetAsync<T>(string sessionId, string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    
    /// <summary>Remove a value from session state.</summary>
    Task RemoveAsync(string sessionId, string key, CancellationToken ct = default);
    
    /// <summary>Clear all state for a session.</summary>
    Task ClearSessionAsync(string sessionId, CancellationToken ct = default);
    
    /// <summary>Check if a key exists.</summary>
    Task<bool> ExistsAsync(string sessionId, string key, CancellationToken ct = default);
}
```

#### Built-in Implementations

```csharp
// In-memory (single instance, development)
services.AddMcpSessionStore<InMemorySessionStore>();

// Distributed cache (Redis, SQL, etc.)
services.AddMcpSessionStore<DistributedCacheSessionStore>();

// Azure Table Storage
services.AddMcpSessionStore<AzureTableSessionStore>(options =>
{
    options.ConnectionString = "...";
    options.TableName = "McpSessions";
    options.DefaultTtl = TimeSpan.FromHours(1);
});
```

#### Access in Tools

```csharp
public class ToolInvocationContext
{
    // ... existing properties ...
    
    /// <summary>Session state store. Null if not configured.</summary>
    public IMcpSessionStore? SessionStore { get; init; }
}
```

#### Usage Example: Multi-Step Wizard

```csharp
[McpTool("order_wizard_start", "Start the order wizard")]
public async Task<object> StartOrderWizard(ToolInvocationContext context)
{
    var wizardState = new OrderWizardState
    {
        Step = 1,
        StartedAt = DateTime.UtcNow
    };
    
    await context.SessionStore!.SetAsync(
        context.SessionId!, 
        "order_wizard", 
        wizardState,
        ttl: TimeSpan.FromMinutes(30));
    
    return new 
    { 
        step = 1, 
        message = "Welcome! What would you like to order?",
        options = await _menuService.GetCategoriesAsync()
    };
}

[McpTool("order_wizard_select", "Select items in the order wizard")]
public async Task<object> SelectItems(
    ToolInvocationContext context,
    [McpToolProperty("items", "array", "Selected item IDs", Required = true)] List<string> items)
{
    var state = await context.SessionStore!.GetAsync<OrderWizardState>(
        context.SessionId!, "order_wizard");
    
    if (state == null)
        return new { error = "No active wizard session. Call order_wizard_start first." };
    
    state.SelectedItems = items;
    state.Step = 2;
    
    await context.SessionStore.SetAsync(context.SessionId!, "order_wizard", state);
    
    return new 
    { 
        step = 2, 
        message = "Great choices! Where should we deliver?",
        selectedItems = items
    };
}

[McpTool("order_wizard_complete", "Complete the order")]
public async Task<object> CompleteOrder(
    ToolInvocationContext context,
    [McpToolProperty("address", "string", "Delivery address", Required = true)] string address)
{
    var state = await context.SessionStore!.GetAsync<OrderWizardState>(
        context.SessionId!, "order_wizard");
    
    if (state?.Step != 2)
        return new { error = "Invalid wizard state" };
    
    // Create the order
    var order = await _orderService.CreateAsync(state.SelectedItems, address, context.User);
    
    // Clean up wizard state
    await context.SessionStore.RemoveAsync(context.SessionId!, "order_wizard");
    
    return new { orderId = order.Id, message = "Order placed successfully!" };
}
```

---

### 12. Tool Execution Middleware

Pre/post execution hooks for cross-cutting concerns: logging, metrics, validation, rate limiting.

#### Interface

```csharp
public interface IMcpToolMiddleware
{
    Task<McpToolResult> InvokeAsync(
        ToolInvocationContext context,
        McpToolDefinition tool,
        Func<Task<McpToolResult>> next);
}
```

#### Registration

```csharp
services.AddMcpServer(mcp =>
{
    // Middleware executes in registration order
    mcp.UseToolMiddleware<LoggingMiddleware>();
    mcp.UseToolMiddleware<MetricsMiddleware>();
    mcp.UseToolMiddleware<RateLimitingMiddleware>();
    mcp.UseToolMiddleware<ValidationMiddleware>();
});
```

#### Built-in Middleware Examples

```csharp
// Logging middleware
public class LoggingMiddleware : IMcpToolMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;
    
    public async Task<McpToolResult> InvokeAsync(
        ToolInvocationContext context,
        McpToolDefinition tool,
        Func<Task<McpToolResult>> next)
    {
        var sw = Stopwatch.StartNew();
        
        _logger.LogInformation("Tool {ToolName} invoked by {User}", 
            tool.Name, context.User?.Identity?.Name ?? "anonymous");
        
        try
        {
            var result = await next();
            
            _logger.LogInformation("Tool {ToolName} completed in {ElapsedMs}ms, IsError={IsError}",
                tool.Name, sw.ElapsedMilliseconds, result.IsError);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {ToolName} failed after {ElapsedMs}ms",
                tool.Name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

// Metrics middleware
public class MetricsMiddleware : IMcpToolMiddleware
{
    private readonly IMeterFactory _meterFactory;
    
    public async Task<McpToolResult> InvokeAsync(
        ToolInvocationContext context,
        McpToolDefinition tool,
        Func<Task<McpToolResult>> next)
    {
        var meter = _meterFactory.Create("Mcp.Tools");
        var counter = meter.CreateCounter<long>("tool_invocations");
        var histogram = meter.CreateHistogram<double>("tool_duration_ms");
        
        var sw = Stopwatch.StartNew();
        var tags = new TagList { { "tool", tool.Name } };
        
        try
        {
            var result = await next();
            tags.Add("status", result.IsError ? "error" : "success");
            return result;
        }
        catch
        {
            tags.Add("status", "exception");
            throw;
        }
        finally
        {
            counter.Add(1, tags);
            histogram.Record(sw.Elapsed.TotalMilliseconds, tags);
        }
    }
}

// Rate limiting middleware
public class RateLimitingMiddleware : IMcpToolMiddleware
{
    private readonly IRateLimiter _limiter;
    
    public async Task<McpToolResult> InvokeAsync(
        ToolInvocationContext context,
        McpToolDefinition tool,
        Func<Task<McpToolResult>> next)
    {
        var key = $"{context.User?.FindFirst("sub")?.Value ?? context.SessionId}:{tool.Name}";
        
        using var lease = await _limiter.AcquireAsync(key, context.CancellationToken);
        
        if (!lease.IsAcquired)
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new[] { new McpTextContent { Text = "Rate limit exceeded. Please try again later." } }
            };
        }
        
        return await next();
    }
}

// Input validation middleware
public class ValidationMiddleware : IMcpToolMiddleware
{
    public async Task<McpToolResult> InvokeAsync(
        ToolInvocationContext context,
        McpToolDefinition tool,
        Func<Task<McpToolResult>> next)
    {
        // Validate arguments against schema
        var validationErrors = ValidateAgainstSchema(context.Arguments, tool.InputSchema);
        
        if (validationErrors.Any())
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new[] { new McpTextContent 
                { 
                    Text = $"Validation failed: {string.Join(", ", validationErrors)}" 
                }}
            };
        }
        
        return await next();
    }
}
```

#### Conditional Middleware

```csharp
// Only apply to specific tools
public class AuditMiddleware : IMcpToolMiddleware
{
    private readonly IAuditLogger _auditLogger;
    private readonly HashSet<string> _auditedTools = new() { "delete_order", "refund_order", "update_pricing" };
    
    public async Task<McpToolResult> InvokeAsync(
        ToolInvocationContext context,
        McpToolDefinition tool,
        Func<Task<McpToolResult>> next)
    {
        if (!_auditedTools.Contains(tool.Name))
            return await next();
        
        await _auditLogger.LogAsync(new AuditEntry
        {
            Tool = tool.Name,
            User = context.User?.FindFirst("sub")?.Value,
            Arguments = context.Arguments,
            Timestamp = DateTime.UtcNow
        });
        
        return await next();
    }
}
```

---

### 13. Resumability

Allow clients to resume after connection drops during long operations.

#### Protocol

Uses `Last-Event-ID` header per SSE spec:

1. Server includes event IDs in SSE stream
2. Client reconnects with `Last-Event-ID` header
3. Server resumes from that point

#### Implementation

```csharp
public class McpServerOptions
{
    // ... existing properties ...
    
    /// <summary>
    /// Enable resumable streams. Requires session storage.
    /// </summary>
    public bool EnableResumability { get; set; } = false;
    
    /// <summary>
    /// How long to keep stream history for resumption.
    /// </summary>
    public TimeSpan ResumabilityWindow { get; set; } = TimeSpan.FromMinutes(5);
}
```

#### Storage Requirements

Resumability requires storing recent events per session. Options:

- In-memory (single instance only)
- Redis/distributed cache (multi-instance)
- Azure Storage Queue (durable)

---

### 14. Sampling (Client-side LLM access)

Server requests to use the client's LLM for inference.

#### Use Case

Server needs LLM capabilities (summarization, extraction, classification) without its own model access.

#### Protocol

Server sends sampling request:
```json
{
  "jsonrpc": "2.0",
  "id": "sample-1",
  "method": "sampling/createMessage",
  "params": {
    "messages": [
      {"role": "user", "content": {"type": "text", "text": "Summarize this order history..."}}
    ],
    "maxTokens": 500
  }
}
```

Client responds with model output:
```json
{
  "jsonrpc": "2.0",
  "id": "sample-1",
  "result": {
    "role": "assistant",
    "content": {"type": "text", "text": "The customer has ordered..."},
    "model": "claude-3-sonnet",
    "stopReason": "end_turn"
  }
}
```

#### Note

Sampling is typically client-initiated. Server-side sampling requires careful security consideration - user must approve sharing LLM access.

---

## Extended Configuration (v2)

```csharp
public class McpServerOptions
{
    // v1 options...
    
    // v2 Transport mode
    public bool EnableSseResponses { get; set; } = false;     // Default: JSON-only (v1 behavior)
    public bool EnableGetSseStream { get; set; } = false;     // Default: GET returns 405 (v1 behavior)
    public TimeSpan SseRetryInterval { get; set; } = TimeSpan.FromSeconds(3);
    
    // v2 Protocol features
    public bool EnablePrompts { get; set; } = true;
    public bool EnableProgress { get; set; } = true;          // Requires EnableSseResponses for streaming
    public bool EnableCancellation { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
    public bool EnableResourceSubscriptions { get; set; } = false;  // Requires EnableGetSseStream
    public bool EnableElicitation { get; set; } = false;            // Requires EnableGetSseStream
    public bool EnableBatchRequests { get; set; } = true;
    public bool EnableResumability { get; set; } = false;     // Requires session storage
    
    // Server card
    public string? ServerDescription { get; set; }
    public string? ServerHomepage { get; set; }
    public string? ServerIcon { get; set; }
    
    // OAuth metadata
    public string[]? AuthorizationServers { get; set; }
    public string[]? ScopesSupported { get; set; }
    
    // Session state
    public bool EnableSessionState { get; set; } = false;
    public TimeSpan DefaultSessionTtl { get; set; } = TimeSpan.FromHours(1);
}
```

---

## Extended DI Setup (v2)

```csharp
public static class McpServiceCollectionExtensions
{
    // v1 methods...
    
    // v2 additions - Prompts
    public static IServiceCollection AddMcpPrompts<T>(
        this IServiceCollection services) where T : class;
    
    // v2 additions - Resources
    public static IServiceCollection AddMcpResourceNotifier<T>(
        this IServiceCollection services) where T : class, IMcpResourceNotifier;
    
    // v2 additions - Session storage
    public static IServiceCollection AddMcpSessionStore<T>(
        this IServiceCollection services) where T : class, IMcpSessionStore;
    
    public static IServiceCollection AddMcpSessionStore<T>(
        this IServiceCollection services, 
        Action<T> configure) where T : class, IMcpSessionStore;
    
    // v2 additions - Middleware
    public static IMcpServerBuilder UseToolMiddleware<T>(
        this IMcpServerBuilder builder) where T : class, IMcpToolMiddleware;
    
    public static IMcpServerBuilder UseToolMiddleware(
        this IMcpServerBuilder builder,
        Func<ToolInvocationContext, McpToolDefinition, Func<Task<McpToolResult>>, Task<McpToolResult>> middleware);
}

// Fluent builder for server configuration
public interface IMcpServerBuilder
{
    // Tool definition (fluent API)
    IMcpToolBuilder DefineTool(string name);
    
    // Resource definition (fluent API)
    IMcpResourceBuilder DefineResource(string uriPattern);
    
    // Prompt definition (fluent API)
    IMcpPromptBuilder DefinePrompt(string name);
    
    // Middleware
    IMcpServerBuilder UseToolMiddleware<T>() where T : class, IMcpToolMiddleware;
    IMcpServerBuilder UseResourceMiddleware<T>() where T : class, IMcpResourceMiddleware;
}
```

#### Full Registration Example (v2)

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddMcpServer(mcp =>
        {
            // Server metadata
            mcp.ServerName = "Muneris POS Server";
            mcp.ServerVersion = "2.0.0";
            mcp.Instructions = "POS management tools for hospitality";
            mcp.ServerDescription = "Full-featured POS integration";
            mcp.ServerHomepage = "https://muneris.dk";
            
            // Features
            mcp.EnablePrompts = true;
            mcp.EnableProgress = true;
            mcp.EnableLogging = true;
            mcp.EnableSessionState = true;
            
            // Security
            mcp.AllowedOrigins = new[] { "https://claude.ai" };
            mcp.AuthorizationServers = new[] { "https://auth.muneris.dk" };
            mcp.ScopesSupported = new[] { "mcp:tools", "mcp:resources" };
        });
        
        // Authentication
        services.AddMcpAuthValidator<JwtBearerValidator>();
        
        // Attribute-based tools/resources/prompts
        services.AddMcpTools<OrderTools>();
        services.AddMcpTools<MenuTools>();
        services.AddMcpResources<MenuResources>();
        services.AddMcpPrompts<SupportPrompts>();
        
        // Session storage
        services.AddMcpSessionStore<AzureTableSessionStore>(store =>
        {
            store.ConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            store.TableName = "McpSessions";
        });
        
        // Fluent tool definitions
        services.AddMcpServer(mcp =>
        {
            mcp.DefineTool("health_check")
               .WithDescription("Server health check")
               .AllowAnonymous()
               .HandledBy(async ctx => new { status = "healthy", timestamp = DateTime.UtcNow });
            
            // Middleware pipeline
            mcp.UseToolMiddleware<LoggingMiddleware>();
            mcp.UseToolMiddleware<MetricsMiddleware>();
            mcp.UseToolMiddleware<RateLimitingMiddleware>();
        });
    })
    .Build();
```

---

## v2 Implementation Checklist

### Phase 0: SSE Transport
- [ ] SSE response format support for POST responses
- [ ] `text/event-stream` content type handling
- [ ] Content negotiation (Accept header parsing)
- [ ] SSE event ID generation
- [ ] GET endpoint returning SSE stream
- [ ] `retry` field handling
- [ ] Connection close without stream termination (polling pattern)

### Phase 1: Prompts
- [ ] McpPromptAttribute, McpPromptArgumentAttribute
- [ ] McpPromptRegistry
- [ ] PromptRequestContext
- [ ] prompts/list handler
- [ ] prompts/get handler

### Phase 2: Progress & Cancellation
- [ ] Progress token handling in ToolInvocationContext
- [ ] ReportProgress callback implementation
- [ ] Progress notifications via SSE stream
- [ ] Cancellation token source tracking
- [ ] notifications/cancelled handler

### Phase 3: Logging
- [ ] IMcpLogger interface
- [ ] Logging capability negotiation
- [ ] Log level filtering
- [ ] notifications/message sending

### Phase 4: Resource Subscriptions
- [ ] IMcpResourceNotifier interface
- [ ] Subscription tracking per session
- [ ] resources/subscribe handler
- [ ] resources/unsubscribe handler
- [ ] notifications/resources/updated sending

### Phase 5: Discovery Endpoints
- [ ] /.well-known/mcp.json handler
- [ ] /.well-known/oauth-protected-resource handler
- [ ] Server card configuration

### Phase 6: Fluent Tool Definition
- [ ] IMcpToolBuilder interface
- [ ] PropertyBuilder for schema configuration
- [ ] IMcpToolHandler interface
- [ ] Runtime tool registration in McpToolRegistry
- [ ] Lambda handler support
- [ ] Full schema override support

### Phase 7: Session State Store
- [ ] IMcpSessionStore interface
- [ ] InMemorySessionStore implementation
- [ ] DistributedCacheSessionStore implementation
- [ ] AzureTableSessionStore implementation (optional)
- [ ] SessionStore in ToolInvocationContext
- [ ] TTL/expiration handling

### Phase 8: Tool Middleware
- [ ] IMcpToolMiddleware interface
- [ ] Middleware pipeline builder
- [ ] Built-in LoggingMiddleware
- [ ] Built-in MetricsMiddleware
- [ ] Conditional middleware support

### Phase 9: Elicitation
- [ ] Elicitation request/response handling
- [ ] ElicitAsync in ToolInvocationContext
- [ ] User approval flow

### Phase 10: Batch & Resumability
- [ ] Batch request parsing
- [ ] Parallel execution with ordering
- [ ] Event ID generation
- [ ] Session storage for resumability
- [ ] Last-Event-ID handling



---

## Priority Assessment

### Transport (Foundation)

| Component | Priority | Complexity | Notes |
|-----------|----------|------------|-------|
| SSE response format | High | Medium | Foundation for progress, notifications |
| GET SSE endpoint | Medium | Low | Required for subscriptions, elicitation |
| Resumability | Low | High | Nice-to-have, requires storage |

### Features

| Feature | Priority | Complexity | Value | Version |
|---------|----------|------------|-------|---------|
| Prompts | High | Medium | High - workflow templates | v2.0 |
| Progress | High | Low | High - essential UX | v2.0 |
| Cancellation | High | Low | Medium - good practice | v2.0 |
| Logging | Medium | Low | Medium - debugging | v2.0 |
| Server Cards | Medium | Low | Medium - discoverability | v2.0 |
| OAuth metadata | Medium | Low | Medium - enterprise auth | v2.0 |
| Fluent tool definition | Medium | Medium | High - runtime scenarios | v2.0 |
| Session state store | Medium | Medium | High - stateful workflows | v2.0 |
| Tool middleware | Medium | Medium | High - cross-cutting concerns | v2.0 |
| Batch requests | Medium | Medium | Medium - efficiency | v2.1 |
| Resource subscriptions | Low | High | Medium - real-time updates | v2.1 |
| Elicitation | Low | High | Low - niche use case | v3.0 |
| Resumability | Low | High | Low - complex, niche | v3.0 |
| Sampling | Low | High | Low - security concerns | v3.0 |
| Client state encryption | Low | Medium | Low - if demanded | v3.0 |

---

## Release Strategy

### v2.0 Scope

**Transport:**
- SSE response format for POST
- GET SSE endpoint

**Features:**
- Prompts
- Progress notifications
- Cancellation
- Logging
- Server Cards (`/.well-known/mcp.json`)
- OAuth metadata endpoint
- Fluent tool definition API
- Session state store
- Tool execution middleware

### v2.1 Scope
- Batch requests
- Resource subscriptions
- Resource middleware

### v3.0 Scope (If Demanded)
- Elicitation (server-initiated user input)
- Resumability (connection recovery)
- Sampling (client LLM access)
- Client state encryption

---

## Migration Path v1 → v2

v2 is fully backward compatible with v1:

1. All v1 features continue to work unchanged
2. JSON-only transport mode remains the default
3. SSE transport is opt-in via configuration
4. New features are opt-in via configuration
5. New attributes are additive (existing tools/resources unchanged)
6. Capability negotiation ensures clients only see supported features
7. POCO binding from v1 continues to work

```csharp
// v1 code continues to work exactly as before
services.AddMcpServer(options =>
{
    options.ServerName = "My Server";
});

services.AddMcpTools<MyTools>();

// Enable v2 transport and features incrementally
services.AddMcpServer(mcp =>
{
    mcp.ServerName = "My Server";
    
    // Transport mode (default: JSON-only like v1)
    mcp.EnableSseResponses = true;      // Allow SSE responses to POST
    mcp.EnableGetSseStream = true;      // Enable GET SSE endpoint
    
    // v2 protocol features
    mcp.EnablePrompts = true;
    mcp.EnableProgress = true;
    mcp.EnableLogging = true;
    
    // v2 infrastructure features
    mcp.EnableSessionState = true;
    
    // Fluent tools alongside attribute-based
    mcp.DefineTool("dynamic_tool")
       .WithDescription("Runtime defined")
       .HandledBy(async ctx => "result");
    
    // Middleware pipeline
    mcp.UseToolMiddleware<LoggingMiddleware>();
});

// Add session storage (required for SSE state, progress, resumability)
services.AddMcpSessionStore<InMemorySessionStore>();
```
