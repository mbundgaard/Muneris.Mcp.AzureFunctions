namespace Muneris.Mcp.AzureFunctions.Extensions;

/// <summary>
/// Configuration options for the MCP server.
/// </summary>
public sealed class McpServerOptions
{
    /// <summary>
    /// The name of the MCP server.
    /// Default is "MCP Server".
    /// </summary>
    public string ServerName { get; set; } = "MCP Server";

    /// <summary>
    /// The version of the MCP server.
    /// Default is "1.0.0".
    /// </summary>
    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Optional instructions for clients about how to use this server.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// List of allowed origins for CORS/DNS rebinding protection.
    /// If empty or null, all origins are allowed.
    /// </summary>
    public List<string>? AllowedOrigins { get; set; }

    /// <summary>
    /// Supported MCP protocol versions.
    /// Default includes "2025-11-25" and "2025-03-26".
    /// </summary>
    public string[] SupportedProtocolVersions { get; set; } = ["2025-11-25", "2025-03-26"];

    /// <summary>
    /// Whether to require the Origin header on all requests.
    /// Default is false (Origin header is optional).
    /// </summary>
    public bool RequireOriginHeader { get; set; }

    /// <summary>
    /// Whether to enable the tools capability.
    /// Default is true.
    /// </summary>
    public bool EnableTools { get; set; } = true;

    /// <summary>
    /// Whether to enable the resources capability.
    /// Default is true.
    /// </summary>
    public bool EnableResources { get; set; } = true;
}

/// <summary>
/// Backwards-compatible alias for McpServerOptions.
/// </summary>
[Obsolete("Use McpServerOptions instead.")]
public sealed class McpOptions
{
    /// <summary>
    /// The name of the MCP server.
    /// Default is "MCP Server".
    /// </summary>
    public string ServerName { get; set; } = "MCP Server";

    /// <summary>
    /// The version of the MCP server.
    /// Default is "1.0.0".
    /// </summary>
    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Optional instructions for clients about how to use this server.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// List of allowed origins for CORS/DNS rebinding protection.
    /// If empty or null, all origins are allowed.
    /// </summary>
    public List<string>? AllowedOrigins { get; set; }
}
