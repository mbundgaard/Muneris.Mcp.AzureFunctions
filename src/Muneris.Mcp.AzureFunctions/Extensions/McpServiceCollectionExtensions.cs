using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muneris.Mcp.AzureFunctions.Auth;
using Muneris.Mcp.AzureFunctions.Services;

namespace Muneris.Mcp.AzureFunctions.Extensions;

/// <summary>
/// Extension methods for configuring MCP services.
/// </summary>
public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCP server services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the MCP builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcp(this IServiceCollection services, Action<McpBuilder> configure)
    {
        var builder = new McpBuilder(services);
        configure(builder);

        // Register McpToolRegistry
        services.TryAddSingleton<McpToolRegistry>(sp =>
        {
            var registry = ActivatorUtilities.CreateInstance<McpToolRegistry>(sp);

            foreach (var assembly in builder.ToolAssemblies)
            {
                registry.RegisterToolsFromAssembly(assembly);
            }

            foreach (var type in builder.ToolTypes)
            {
                registry.RegisterToolsFromType(type);
            }

            return registry;
        });

        // Register McpResourceRegistry if any resources are configured
        if (builder.ResourceAssemblies.Count > 0 || builder.ResourceTypes.Count > 0)
        {
            services.TryAddSingleton<McpResourceRegistry>(sp =>
            {
                var registry = ActivatorUtilities.CreateInstance<McpResourceRegistry>(sp);

                foreach (var assembly in builder.ResourceAssemblies)
                {
                    registry.RegisterResourcesFromAssembly(assembly);
                }

                foreach (var type in builder.ResourceTypes)
                {
                    registry.RegisterResourcesFromType(type);
                }

                return registry;
            });
        }

        services.TryAddSingleton<McpRequestHandler>();

        return services;
    }

    /// <summary>
    /// Adds MCP server services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcp(this IServiceCollection services)
    {
        return services.AddMcp(_ => { });
    }

    /// <summary>
    /// Adds MCP server services with options configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure server options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpServer(this IServiceCollection services, Action<McpServerOptions>? configureOptions = null)
    {
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        services.TryAddSingleton<McpToolRegistry>(sp =>
        {
            return ActivatorUtilities.CreateInstance<McpToolRegistry>(sp);
        });

        services.TryAddSingleton<McpResourceRegistry>(sp =>
        {
            return ActivatorUtilities.CreateInstance<McpResourceRegistry>(sp);
        });

        services.TryAddSingleton<McpRequestHandler>();

        return services;
    }

    /// <summary>
    /// Adds an authentication validator to the MCP server.
    /// </summary>
    /// <typeparam name="TValidator">The validator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpAuthValidator<TValidator>(this IServiceCollection services)
        where TValidator : class, IMcpAuthValidator
    {
        services.AddSingleton<IMcpAuthValidator, TValidator>();
        return services;
    }

    /// <summary>
    /// Adds tools from the specified type to the MCP server.
    /// </summary>
    /// <typeparam name="TTools">The type containing tools.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpTools<TTools>(this IServiceCollection services)
        where TTools : class
    {
        // Register the tools type for DI
        services.TryAddTransient<TTools>();

        // Get or create the registry and register tools from this type
        services.AddSingleton<IConfigureToolsAction>(new ConfigureToolsAction<TTools>());

        return services;
    }

    /// <summary>
    /// Adds resources from the specified type to the MCP server.
    /// </summary>
    /// <typeparam name="TResources">The type containing resources.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpResources<TResources>(this IServiceCollection services)
        where TResources : class
    {
        // Register the resources type for DI
        services.TryAddTransient<TResources>();

        // Get or create the registry and register resources from this type
        services.AddSingleton<IConfigureResourcesAction>(new ConfigureResourcesAction<TResources>());

        return services;
    }
}

/// <summary>
/// Internal interface for deferred tool registration.
/// </summary>
internal interface IConfigureToolsAction
{
    void Configure(McpToolRegistry registry);
}

/// <summary>
/// Internal interface for deferred resource registration.
/// </summary>
internal interface IConfigureResourcesAction
{
    void Configure(McpResourceRegistry registry);
}

internal sealed class ConfigureToolsAction<T> : IConfigureToolsAction
{
    public void Configure(McpToolRegistry registry) => registry.RegisterToolsFromType(typeof(T));
}

internal sealed class ConfigureResourcesAction<T> : IConfigureResourcesAction
{
    public void Configure(McpResourceRegistry registry) => registry.RegisterResourcesFromType(typeof(T));
}
