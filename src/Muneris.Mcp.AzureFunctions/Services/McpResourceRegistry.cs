using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muneris.Mcp.AzureFunctions.Attributes;
using Muneris.Mcp.AzureFunctions.Models.Resources;

namespace Muneris.Mcp.AzureFunctions.Services;

/// <summary>
/// Information about a registered MCP resource.
/// </summary>
internal sealed class RegisteredResource
{
    public required string UriPattern { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string MimeType { get; init; }
    public bool AllowAnonymous { get; init; }
    public required MethodInfo Method { get; init; }
    public required Type DeclaringType { get; init; }

    /// <summary>
    /// The regex pattern for matching URIs against this resource.
    /// </summary>
    public required Regex UriRegex { get; init; }

    /// <summary>
    /// The parameter names extracted from the URI pattern.
    /// </summary>
    public required List<string> ParameterNames { get; init; }
}

/// <summary>
/// Information about a resource list provider.
/// </summary>
internal sealed class RegisteredResourceListProvider
{
    public required string Scheme { get; init; }
    public required MethodInfo Method { get; init; }
    public required Type DeclaringType { get; init; }
}

/// <summary>
/// Registry for discovering and invoking MCP resources.
/// </summary>
public sealed class McpResourceRegistry
{
    private readonly Dictionary<string, RegisteredResource> _resources = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RegisteredResourceListProvider> _listProviders = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpResourceRegistry> _logger;

    /// <summary>
    /// Creates a new resource registry.
    /// </summary>
    public McpResourceRegistry(IServiceProvider serviceProvider, ILogger<McpResourceRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Registers all resources from the specified assembly.
    /// </summary>
    public void RegisterResourcesFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes();
        foreach (var type in types)
        {
            RegisterResourcesFromType(type);
        }
    }

    /// <summary>
    /// Registers all resources from the specified type.
    /// </summary>
    public void RegisterResourcesFromType(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var method in methods)
        {
            // Check for McpResourceAttribute
            var resourceAttr = method.GetCustomAttribute<McpResourceAttribute>();
            if (resourceAttr is not null)
            {
                RegisterResource(type, method, resourceAttr);
            }

            // Check for McpResourceListAttribute
            var listAttr = method.GetCustomAttribute<McpResourceListAttribute>();
            if (listAttr is not null)
            {
                RegisterResourceListProvider(type, method, listAttr);
            }
        }
    }

    private void RegisterResource(Type type, MethodInfo method, McpResourceAttribute attr)
    {
        var (regex, paramNames) = BuildUriPattern(attr.UriPattern);

        var resource = new RegisteredResource
        {
            UriPattern = attr.UriPattern,
            Name = attr.Name,
            Description = attr.Description,
            MimeType = attr.MimeType,
            AllowAnonymous = attr.AllowAnonymous,
            Method = method,
            DeclaringType = type,
            UriRegex = regex,
            ParameterNames = paramNames
        };

        _resources[attr.UriPattern] = resource;
        _logger.LogDebug("Registered MCP resource: {UriPattern} from {Type}.{Method}",
            attr.UriPattern, type.Name, method.Name);
    }

    private void RegisterResourceListProvider(Type type, MethodInfo method, McpResourceListAttribute attr)
    {
        var provider = new RegisteredResourceListProvider
        {
            Scheme = attr.Scheme,
            Method = method,
            DeclaringType = type
        };

        _listProviders.Add(provider);
        _logger.LogDebug("Registered MCP resource list provider for scheme '{Scheme}' from {Type}.{Method}",
            attr.Scheme, type.Name, method.Name);
    }

    private static (Regex regex, List<string> paramNames) BuildUriPattern(string uriPattern)
    {
        var paramNames = new List<string>();

        // Extract parameter names and build regex
        // Example: "menu://categories/{categoryId}" becomes "^menu://categories/(?<categoryId>[^/]+)$"
        var regexPattern = Regex.Replace(uriPattern, @"\{(\w+)\}", match =>
        {
            var paramName = match.Groups[1].Value;
            paramNames.Add(paramName);
            return $"(?<{paramName}>[^/]+)";
        });

        // Escape special regex characters in the static parts (except our named groups)
        regexPattern = "^" + Regex.Escape(uriPattern)
            .Replace(@"\{", "{")
            .Replace(@"\}", "}");

        // Now replace {param} with capture groups
        regexPattern = Regex.Replace(regexPattern, @"\{(\w+)\}", match =>
        {
            return $"(?<{match.Groups[1].Value}>[^/]+)";
        }) + "$";

        return (new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase), paramNames);
    }

    /// <summary>
    /// Gets all static resource definitions (from [McpResource] attributes).
    /// </summary>
    public IReadOnlyList<McpResourceInfo> GetStaticResourceDefinitions()
    {
        return _resources.Values.Select(r => new McpResourceInfo
        {
            Uri = r.UriPattern,
            Name = r.Name,
            Description = r.Description,
            MimeType = r.MimeType
        }).ToList();
    }

    /// <summary>
    /// Gets all resources, including dynamically discovered ones from list providers.
    /// </summary>
    public async Task<IReadOnlyList<McpResourceInfo>> GetAllResourcesAsync(
        ClaimsPrincipal? user,
        string? sessionId,
        string? protocolVersion,
        HttpRequestData request,
        CancellationToken cancellationToken = default)
    {
        var resources = new List<McpResourceInfo>();

        // Add static resources
        resources.AddRange(GetStaticResourceDefinitions());

        // Add dynamic resources from list providers
        foreach (var provider in _listProviders)
        {
            try
            {
                var context = new ResourceRequestContext(
                    string.Empty,
                    new Dictionary<string, string>(),
                    user,
                    sessionId,
                    protocolVersion,
                    request,
                    cancellationToken);

                var dynamicResources = await InvokeListProviderAsync(provider, context);
                if (dynamicResources is not null)
                {
                    resources.AddRange(dynamicResources);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error invoking resource list provider for scheme '{Scheme}'", provider.Scheme);
            }
        }

        return resources;
    }

    private async Task<IEnumerable<McpResourceInfo>?> InvokeListProviderAsync(
        RegisteredResourceListProvider provider,
        ResourceRequestContext context)
    {
        var instance = GetOrCreateInstance(provider.DeclaringType, provider.Method);
        var parameters = provider.Method.GetParameters();
        var args = BuildMethodArguments(parameters, context);

        var result = provider.Method.Invoke(instance, args);

        if (result is Task task)
        {
            await task;
            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProperty = taskType.GetProperty("Result");
                result = resultProperty?.GetValue(task);
            }
            else
            {
                return null;
            }
        }

        return result as IEnumerable<McpResourceInfo>;
    }

    /// <summary>
    /// Checks if any resource matches the given URI.
    /// </summary>
    public bool HasResource(string uri)
    {
        return _resources.Values.Any(r => r.UriRegex.IsMatch(uri));
    }

    /// <summary>
    /// Checks if a matching resource allows anonymous access.
    /// </summary>
    public bool IsResourceAnonymous(string uri)
    {
        var resource = _resources.Values.FirstOrDefault(r => r.UriRegex.IsMatch(uri));
        return resource?.AllowAnonymous ?? false;
    }

    /// <summary>
    /// Reads a resource by URI.
    /// </summary>
    public async Task<McpResourceResult?> ReadResourceAsync(
        string uri,
        ClaimsPrincipal? user,
        string? sessionId,
        string? protocolVersion,
        HttpRequestData request,
        CancellationToken cancellationToken = default)
    {
        var resource = _resources.Values.FirstOrDefault(r => r.UriRegex.IsMatch(uri));
        if (resource is null)
        {
            return null;
        }

        // Extract parameters from URI
        var match = resource.UriRegex.Match(uri);
        var parameters = new Dictionary<string, string>();
        foreach (var paramName in resource.ParameterNames)
        {
            var group = match.Groups[paramName];
            if (group.Success)
            {
                parameters[paramName] = group.Value;
            }
        }

        var context = new ResourceRequestContext(
            uri,
            parameters,
            user,
            sessionId,
            protocolVersion,
            request,
            cancellationToken);

        return await InvokeResourceAsync(resource, context);
    }

    private async Task<McpResourceResult?> InvokeResourceAsync(RegisteredResource resource, ResourceRequestContext context)
    {
        var instance = GetOrCreateInstance(resource.DeclaringType, resource.Method);
        var parameters = resource.Method.GetParameters();
        var args = BuildMethodArguments(parameters, context);

        var result = resource.Method.Invoke(instance, args);

        if (result is Task task)
        {
            await task;
            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProperty = taskType.GetProperty("Result");
                result = resultProperty?.GetValue(task);
            }
            else
            {
                return null;
            }
        }

        return result as McpResourceResult;
    }

    private object? GetOrCreateInstance(Type declaringType, MethodInfo method)
    {
        if (method.IsStatic)
        {
            return null;
        }

        var instance = _serviceProvider.GetService(declaringType);
        if (instance is null)
        {
            instance = ActivatorUtilities.CreateInstance(_serviceProvider, declaringType);
        }
        return instance;
    }

    private static object?[] BuildMethodArguments(ParameterInfo[] parameters, ResourceRequestContext context)
    {
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            if (param.ParameterType == typeof(ResourceRequestContext))
            {
                args[i] = context;
            }
            else if (param.ParameterType == typeof(CancellationToken))
            {
                args[i] = context.CancellationToken;
            }
            else if (param.HasDefaultValue)
            {
                args[i] = param.DefaultValue;
            }
            else if (param.ParameterType.IsValueType)
            {
                args[i] = Activator.CreateInstance(param.ParameterType);
            }
            else
            {
                args[i] = null;
            }
        }

        return args;
    }

    /// <summary>
    /// Returns true if there are any registered resources.
    /// </summary>
    public bool HasAnyResources => _resources.Count > 0 || _listProviders.Count > 0;
}
