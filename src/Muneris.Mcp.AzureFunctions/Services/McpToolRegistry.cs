using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muneris.Mcp.AzureFunctions.Attributes;
using Muneris.Mcp.AzureFunctions.Models;

namespace Muneris.Mcp.AzureFunctions.Services;

/// <summary>
/// Information about a registered MCP tool.
/// </summary>
internal sealed class RegisteredTool
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool AllowAnonymous { get; init; }
    public required MethodInfo Method { get; init; }
    public required Type DeclaringType { get; init; }
    public required List<McpToolPropertyAttribute> Properties { get; init; }
    public required McpToolDefinition Definition { get; init; }

    /// <summary>
    /// The POCO parameter type, if the tool uses POCO binding.
    /// </summary>
    public Type? PocoParameterType { get; init; }

    /// <summary>
    /// The parameter index for the POCO parameter, if applicable.
    /// </summary>
    public int PocoParameterIndex { get; init; } = -1;
}

/// <summary>
/// Registry for discovering and invoking MCP tools.
/// </summary>
public sealed class McpToolRegistry
{
    private readonly Dictionary<string, RegisteredTool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpToolRegistry> _logger;

    /// <summary>
    /// Creates a new tool registry.
    /// </summary>
    public McpToolRegistry(IServiceProvider serviceProvider, ILogger<McpToolRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Registers all tools from the specified assembly.
    /// </summary>
    public void RegisterToolsFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes();
        foreach (var type in types)
        {
            RegisterToolsFromType(type);
        }
    }

    /// <summary>
    /// Registers all tools from the specified type.
    /// </summary>
    public void RegisterToolsFromType(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var method in methods)
        {
            var toolAttr = method.GetCustomAttribute<McpToolAttribute>();
            if (toolAttr is null) continue;

            RegisterTool(type, method, toolAttr);
        }
    }

    private void RegisterTool(Type type, MethodInfo method, McpToolAttribute toolAttr)
    {
        var properties = method.GetCustomAttributes<McpToolPropertyAttribute>().ToList();

        // Detect POCO parameter
        Type? pocoType = null;
        int pocoIndex = -1;
        var parameters = method.GetParameters();

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            if (param.ParameterType == typeof(ToolInvocationContext)) continue;
            if (param.ParameterType == typeof(CancellationToken)) continue;

            // Check if this is a POCO type
            if (SchemaGenerator.IsPocoType(param.ParameterType))
            {
                pocoType = param.ParameterType;
                pocoIndex = i;
                break;
            }
        }

        var definition = BuildToolDefinition(toolAttr, properties, method, pocoType);

        var tool = new RegisteredTool
        {
            Name = toolAttr.Name,
            Description = toolAttr.Description,
            AllowAnonymous = toolAttr.AllowAnonymous,
            Method = method,
            DeclaringType = type,
            Properties = properties,
            Definition = definition,
            PocoParameterType = pocoType,
            PocoParameterIndex = pocoIndex
        };

        _tools[toolAttr.Name] = tool;
        _logger.LogDebug("Registered MCP tool: {ToolName} from {Type}.{Method}{PocoInfo}",
            toolAttr.Name, type.Name, method.Name,
            pocoType is not null ? $" (POCO: {pocoType.Name})" : "");
    }

    private static McpToolDefinition BuildToolDefinition(
        McpToolAttribute toolAttr,
        List<McpToolPropertyAttribute> properties,
        MethodInfo method,
        Type? pocoType)
    {
        var definition = new McpToolDefinition
        {
            Name = toolAttr.Name,
            Description = toolAttr.Description,
            InputSchema = new McpToolInputSchema()
        };

        // Add annotations if any are set
        var annotations = BuildAnnotations(toolAttr);
        if (annotations.HasAnnotations)
        {
            definition.Annotations = annotations;
        }

        // If we have a POCO type, generate schema from it
        if (pocoType is not null)
        {
            var pocoSchema = SchemaGenerator.GenerateObjectSchema(pocoType);
            definition.InputSchema.Properties = pocoSchema.Properties ?? new Dictionary<string, McpToolPropertySchema>();
            definition.InputSchema.Required = pocoSchema.Required;
        }
        else
        {
            // Use attribute-based properties
            var required = new List<string>();

            foreach (var prop in properties)
            {
                var propSchema = new McpToolPropertySchema
                {
                    Type = prop.Type,
                    Description = prop.Description
                };

                // Apply enhanced schema properties from attribute
                if (prop.Default is not null)
                {
                    propSchema.Default = prop.Default;
                }
                if (prop.Enum is not null && prop.Enum.Length > 0)
                {
                    propSchema.Enum = prop.Enum;
                }
                if (!string.IsNullOrEmpty(prop.Format))
                {
                    propSchema.Format = prop.Format;
                }
                if (!double.IsNaN(prop.Minimum))
                {
                    propSchema.Minimum = prop.Minimum;
                }
                if (!double.IsNaN(prop.Maximum))
                {
                    propSchema.Maximum = prop.Maximum;
                }
                if (prop.MinLength >= 0)
                {
                    propSchema.MinLength = prop.MinLength;
                }
                if (prop.MaxLength >= 0)
                {
                    propSchema.MaxLength = prop.MaxLength;
                }
                if (!string.IsNullOrEmpty(prop.Pattern))
                {
                    propSchema.Pattern = prop.Pattern;
                }

                definition.InputSchema.Properties[prop.Name] = propSchema;

                if (prop.Required)
                {
                    required.Add(prop.Name);
                }
            }

            if (required.Count > 0)
            {
                definition.InputSchema.Required = required;
            }
        }

        return definition;
    }

    private static McpToolAnnotations BuildAnnotations(McpToolAttribute toolAttr)
    {
        var annotations = new McpToolAnnotations();

        if (!string.IsNullOrEmpty(toolAttr.Title))
        {
            annotations.Title = toolAttr.Title;
        }

        // Only set hints if they differ from defaults
        if (toolAttr.ReadOnlyHint)
        {
            annotations.ReadOnlyHint = true;
        }

        if (toolAttr.DestructiveHint)
        {
            annotations.DestructiveHint = true;
        }

        if (toolAttr.IdempotentHint)
        {
            annotations.IdempotentHint = true;
        }

        // OpenWorldHint defaults to true, so only set if false
        if (!toolAttr.OpenWorldHint)
        {
            annotations.OpenWorldHint = false;
        }

        return annotations;
    }

    /// <summary>
    /// Gets all registered tool definitions.
    /// </summary>
    public IReadOnlyList<McpToolDefinition> GetToolDefinitions()
    {
        return _tools.Values.Select(t => t.Definition).ToList();
    }

    /// <summary>
    /// Checks if a tool exists.
    /// </summary>
    public bool HasTool(string name) => _tools.ContainsKey(name);

    /// <summary>
    /// Checks if a tool allows anonymous access.
    /// </summary>
    public bool IsToolAnonymous(string name)
    {
        return _tools.TryGetValue(name, out var tool) && tool.AllowAnonymous;
    }

    /// <summary>
    /// Invokes a tool with the given context.
    /// </summary>
    public async Task<object?> InvokeToolAsync(ToolInvocationContext context)
    {
        if (!_tools.TryGetValue(context.ToolName, out var tool))
        {
            throw new InvalidOperationException($"Tool not found: {context.ToolName}");
        }

        object? instance = null;
        if (!tool.Method.IsStatic)
        {
            instance = _serviceProvider.GetService(tool.DeclaringType);
            if (instance is null)
            {
                instance = ActivatorUtilities.CreateInstance(_serviceProvider, tool.DeclaringType);
            }
        }

        var parameters = tool.Method.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            if (param.ParameterType == typeof(ToolInvocationContext))
            {
                args[i] = context;
            }
            else if (param.ParameterType == typeof(CancellationToken))
            {
                args[i] = context.CancellationToken;
            }
            else if (tool.PocoParameterType is not null && i == tool.PocoParameterIndex)
            {
                // Deserialize the entire arguments object to the POCO type
                if (context.Arguments is not null && context.Arguments.Value.ValueKind == JsonValueKind.Object)
                {
                    args[i] = JsonSerializer.Deserialize(context.Arguments.Value.GetRawText(), tool.PocoParameterType, JsonOptions);
                }
                else
                {
                    args[i] = Activator.CreateInstance(tool.PocoParameterType);
                }
            }
            else if (context.Arguments is not null &&
                     context.Arguments.Value.ValueKind == JsonValueKind.Object &&
                     context.Arguments.Value.TryGetProperty(param.Name!, out var value))
            {
                args[i] = DeserializeParameter(value, param.ParameterType);
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

        var result = tool.Method.Invoke(instance, args);

        if (result is Task task)
        {
            await task;
            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProperty = taskType.GetProperty("Result");
                return resultProperty?.GetValue(task);
            }
            return null;
        }

        return result;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static object? DeserializeParameter(JsonElement element, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        }

        if (targetType == typeof(int) || targetType == typeof(int?))
        {
            return element.ValueKind == JsonValueKind.Number ? element.GetInt32() : null;
        }

        if (targetType == typeof(long) || targetType == typeof(long?))
        {
            return element.ValueKind == JsonValueKind.Number ? element.GetInt64() : null;
        }

        if (targetType == typeof(double) || targetType == typeof(double?))
        {
            return element.ValueKind == JsonValueKind.Number ? element.GetDouble() : null;
        }

        if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        if (targetType == typeof(JsonElement))
        {
            return element;
        }

        return JsonSerializer.Deserialize(element.GetRawText(), targetType, JsonOptions);
    }
}
