using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Muneris.Mcp.AzureFunctions.Auth;

namespace Muneris.Mcp.AzureFunctions.Extensions;

/// <summary>
/// Builder for configuring MCP services.
/// </summary>
/// <example>
/// <para><b>Full configuration example:</b></para>
/// <code>
/// services.AddMcp(mcp =&gt;
/// {
///     // Configure server options
///     mcp.Configure(options =&gt;
///     {
///         options.ServerName = "Restaurant MCP Server";
///         options.ServerVersion = "1.0.0";
///         options.Instructions = "This server provides tools for managing restaurant operations.";
///         options.AllowedOrigins = new List&lt;string&gt; { "https://myapp.com" };
///     });
///
///     // Register tools from specific types
///     mcp.AddToolsFromType&lt;OrderTools&gt;();
///     mcp.AddToolsFromType&lt;MenuTools&gt;();
///
///     // Or register all tools from an assembly
///     mcp.AddToolsFromAssemblyContaining&lt;OrderTools&gt;();
///
///     // Register resources
///     mcp.AddResourcesFromType&lt;MenuResources&gt;();
///     mcp.AddResourcesFromType&lt;ConfigResources&gt;();
///
///     // Register authentication validator
///     mcp.AddAuthValidator&lt;JwtBearerValidator&gt;();
///
///     // Or use a factory for complex initialization
///     mcp.AddAuthValidator(sp =&gt; new JwtBearerValidator(
///         sp.GetRequiredService&lt;IConfiguration&gt;(),
///         sp.GetRequiredService&lt;ILogger&lt;JwtBearerValidator&gt;&gt;()));
/// });
/// </code>
/// </example>
public sealed class McpBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Assembly> _toolAssemblies = new();
    private readonly List<Type> _toolTypes = new();
    private readonly List<Assembly> _resourceAssemblies = new();
    private readonly List<Type> _resourceTypes = new();

    internal McpBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Configures MCP server options.
    /// </summary>
    public McpBuilder Configure(Action<McpServerOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Registers tools from the specified assembly.
    /// </summary>
    public McpBuilder AddToolsFromAssembly(Assembly assembly)
    {
        _toolAssemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Registers tools from the assembly containing the specified type.
    /// </summary>
    public McpBuilder AddToolsFromAssemblyContaining<T>()
    {
        return AddToolsFromAssembly(typeof(T).Assembly);
    }

    /// <summary>
    /// Registers tools from the specified type.
    /// </summary>
    public McpBuilder AddToolsFromType<T>()
    {
        _toolTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Registers tools from the specified type.
    /// </summary>
    public McpBuilder AddToolsFromType(Type type)
    {
        _toolTypes.Add(type);
        return this;
    }

    /// <summary>
    /// Registers resources from the specified assembly.
    /// </summary>
    public McpBuilder AddResourcesFromAssembly(Assembly assembly)
    {
        _resourceAssemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Registers resources from the assembly containing the specified type.
    /// </summary>
    public McpBuilder AddResourcesFromAssemblyContaining<T>()
    {
        return AddResourcesFromAssembly(typeof(T).Assembly);
    }

    /// <summary>
    /// Registers resources from the specified type.
    /// </summary>
    public McpBuilder AddResourcesFromType<T>()
    {
        _resourceTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Registers resources from the specified type.
    /// </summary>
    public McpBuilder AddResourcesFromType(Type type)
    {
        _resourceTypes.Add(type);
        return this;
    }

    /// <summary>
    /// Registers an authentication validator.
    /// </summary>
    public McpBuilder AddAuthValidator<TValidator>() where TValidator : class, IMcpAuthValidator
    {
        _services.AddSingleton<IMcpAuthValidator, TValidator>();
        return this;
    }

    /// <summary>
    /// Registers an authentication validator instance.
    /// </summary>
    public McpBuilder AddAuthValidator(IMcpAuthValidator validator)
    {
        _services.AddSingleton(validator);
        return this;
    }

    /// <summary>
    /// Registers an authentication validator using a factory.
    /// </summary>
    public McpBuilder AddAuthValidator(Func<IServiceProvider, IMcpAuthValidator> factory)
    {
        _services.AddSingleton(factory);
        return this;
    }

    internal IReadOnlyList<Assembly> ToolAssemblies => _toolAssemblies;
    internal IReadOnlyList<Type> ToolTypes => _toolTypes;
    internal IReadOnlyList<Assembly> ResourceAssemblies => _resourceAssemblies;
    internal IReadOnlyList<Type> ResourceTypes => _resourceTypes;
}
