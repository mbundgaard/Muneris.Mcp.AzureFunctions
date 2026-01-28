using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Muneris.Mcp.AzureFunctions.Attributes;
using Muneris.Mcp.AzureFunctions.Models;

namespace Muneris.Mcp.AzureFunctions.Services;

/// <summary>
/// Generates JSON Schema from C# types using reflection and DataAnnotations.
/// </summary>
public static class SchemaGenerator
{
    /// <summary>
    /// Generates a JSON Schema for the specified type.
    /// </summary>
    /// <param name="type">The type to generate schema for.</param>
    /// <param name="depth">Current recursion depth (to prevent infinite loops).</param>
    /// <returns>A property schema representing the type.</returns>
    public static McpToolPropertySchema GenerateSchema(Type type, int depth = 0)
    {
        const int maxDepth = 3;

        // Unwrap nullable types
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType is not null)
        {
            type = underlyingType;
        }

        // Handle primitive types
        if (type == typeof(string))
        {
            return new McpToolPropertySchema { Type = "string" };
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            return new McpToolPropertySchema { Type = "integer" };
        }

        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            return new McpToolPropertySchema { Type = "number" };
        }

        if (type == typeof(bool))
        {
            return new McpToolPropertySchema { Type = "boolean" };
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return new McpToolPropertySchema { Type = "string", Format = "date-time" };
        }

        if (type == typeof(DateOnly))
        {
            return new McpToolPropertySchema { Type = "string", Format = "date" };
        }

        if (type == typeof(TimeOnly))
        {
            return new McpToolPropertySchema { Type = "string", Format = "time" };
        }

        if (type == typeof(Guid))
        {
            return new McpToolPropertySchema { Type = "string", Format = "uuid" };
        }

        if (type == typeof(Uri))
        {
            return new McpToolPropertySchema { Type = "string", Format = "uri" };
        }

        // Handle enums
        if (type.IsEnum)
        {
            return new McpToolPropertySchema
            {
                Type = "string",
                Enum = System.Enum.GetNames(type)
            };
        }

        // Handle arrays and collections
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            return new McpToolPropertySchema
            {
                Type = "array",
                Items = depth < maxDepth ? GenerateSchema(elementType, depth + 1) : new McpToolPropertySchema { Type = "object" }
            };
        }

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IEnumerable<>))
            {
                var elementType = type.GetGenericArguments()[0];
                return new McpToolPropertySchema
                {
                    Type = "array",
                    Items = depth < maxDepth ? GenerateSchema(elementType, depth + 1) : new McpToolPropertySchema { Type = "object" }
                };
            }

            // Handle Dictionary as object
            if (genericDef == typeof(Dictionary<,>) || genericDef == typeof(IDictionary<,>))
            {
                return new McpToolPropertySchema { Type = "object" };
            }
        }

        // Handle complex objects
        if (type.IsClass && depth < maxDepth)
        {
            return GenerateObjectSchema(type, depth);
        }

        // Fallback to object
        return new McpToolPropertySchema { Type = "object" };
    }

    /// <summary>
    /// Generates a JSON Schema for a complex object type.
    /// </summary>
    public static McpToolPropertySchema GenerateObjectSchema(Type type, int depth = 0)
    {
        var schema = new McpToolPropertySchema
        {
            Type = "object",
            Properties = new Dictionary<string, McpToolPropertySchema>(),
            Required = new List<string>()
        };

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (!prop.CanRead) continue;

            var propSchema = GeneratePropertySchema(prop, depth);
            var propName = GetPropertyName(prop);

            schema.Properties[propName] = propSchema;

            // Check if required
            if (IsPropertyRequired(prop))
            {
                schema.Required.Add(propName);
            }
        }

        if (schema.Required.Count == 0)
        {
            schema.Required = null;
        }

        return schema;
    }

    /// <summary>
    /// Generates a JSON Schema for a single property, including DataAnnotations.
    /// </summary>
    public static McpToolPropertySchema GeneratePropertySchema(PropertyInfo property, int depth = 0)
    {
        var baseSchema = GenerateSchema(property.PropertyType, depth + 1);

        // Apply DataAnnotations
        ApplyDescriptionAttribute(property, baseSchema);
        ApplyRangeAttribute(property, baseSchema);
        ApplyStringLengthAttributes(property, baseSchema);
        ApplyRegularExpressionAttribute(property, baseSchema);
        ApplyAllowedValuesAttributes(property, baseSchema);
        ApplyFormatAttributes(property, baseSchema);
        ApplyDefaultValue(property, baseSchema);

        return baseSchema;
    }

    private static string GetPropertyName(PropertyInfo property)
    {
        // Use camelCase by default
        var name = property.Name;
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static bool IsPropertyRequired(PropertyInfo property)
    {
        // Check for [Required] attribute
        if (property.GetCustomAttribute<RequiredAttribute>() is not null)
        {
            return true;
        }

        // Non-nullable reference types are required (when NRT is enabled)
        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(property);
        if (nullabilityInfo.WriteState == NullabilityState.NotNull)
        {
            // But only if it's a reference type and doesn't have a default value
            if (!property.PropertyType.IsValueType)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyDescriptionAttribute(PropertyInfo property, McpToolPropertySchema schema)
    {
        var descAttr = property.GetCustomAttribute<DescriptionAttribute>();
        if (descAttr is not null)
        {
            schema.Description = descAttr.Description;
        }
    }

    private static void ApplyRangeAttribute(PropertyInfo property, McpToolPropertySchema schema)
    {
        var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
        if (rangeAttr is not null)
        {
            if (rangeAttr.Minimum is not null)
            {
                schema.Minimum = Convert.ToDouble(rangeAttr.Minimum);
            }
            if (rangeAttr.Maximum is not null)
            {
                schema.Maximum = Convert.ToDouble(rangeAttr.Maximum);
            }
        }
    }

    private static void ApplyStringLengthAttributes(PropertyInfo property, McpToolPropertySchema schema)
    {
        var minLenAttr = property.GetCustomAttribute<MinLengthAttribute>();
        if (minLenAttr is not null)
        {
            schema.MinLength = minLenAttr.Length;
        }

        var maxLenAttr = property.GetCustomAttribute<MaxLengthAttribute>();
        if (maxLenAttr is not null)
        {
            schema.MaxLength = maxLenAttr.Length;
        }

        var strLenAttr = property.GetCustomAttribute<StringLengthAttribute>();
        if (strLenAttr is not null)
        {
            if (strLenAttr.MinimumLength > 0)
            {
                schema.MinLength = strLenAttr.MinimumLength;
            }
            schema.MaxLength = strLenAttr.MaximumLength;
        }
    }

    private static void ApplyRegularExpressionAttribute(PropertyInfo property, McpToolPropertySchema schema)
    {
        var regexAttr = property.GetCustomAttribute<RegularExpressionAttribute>();
        if (regexAttr is not null)
        {
            schema.Pattern = regexAttr.Pattern;
        }
    }

    private static void ApplyAllowedValuesAttributes(PropertyInfo property, McpToolPropertySchema schema)
    {
        // Check for McpAllowedValuesAttribute
        var mcpAllowedAttr = property.GetCustomAttribute<McpAllowedValuesAttribute>();
        if (mcpAllowedAttr is not null && mcpAllowedAttr.Values.Length > 0)
        {
            schema.Enum = mcpAllowedAttr.Values.Select(v => v?.ToString() ?? "").ToArray();
            return;
        }

        // Check for .NET 8's AllowedValuesAttribute
        var allowedAttr = property.GetCustomAttribute<AllowedValuesAttribute>();
        if (allowedAttr is not null && allowedAttr.Values.Length > 0)
        {
            schema.Enum = allowedAttr.Values.Select(v => v?.ToString() ?? "").ToArray();
        }
    }

    private static void ApplyFormatAttributes(PropertyInfo property, McpToolPropertySchema schema)
    {
        // EmailAddress
        if (property.GetCustomAttribute<EmailAddressAttribute>() is not null)
        {
            schema.Format = "email";
            return;
        }

        // Url
        if (property.GetCustomAttribute<UrlAttribute>() is not null)
        {
            schema.Format = "uri";
            return;
        }

        // Phone
        if (property.GetCustomAttribute<PhoneAttribute>() is not null)
        {
            schema.Format = "phone";
            return;
        }

        // DataType
        var dataTypeAttr = property.GetCustomAttribute<DataTypeAttribute>();
        if (dataTypeAttr is not null)
        {
            schema.Format = dataTypeAttr.DataType switch
            {
                DataType.Date => "date",
                DataType.DateTime => "date-time",
                DataType.Time => "time",
                DataType.EmailAddress => "email",
                DataType.Url => "uri",
                DataType.PhoneNumber => "phone",
                _ => schema.Format
            };
        }
    }

    private static void ApplyDefaultValue(PropertyInfo property, McpToolPropertySchema schema)
    {
        var defaultAttr = property.GetCustomAttribute<DefaultValueAttribute>();
        if (defaultAttr is not null)
        {
            schema.Default = defaultAttr.Value;
        }
    }

    /// <summary>
    /// Determines if a type is a POCO (complex object) that should have schema generated.
    /// </summary>
    public static bool IsPocoType(Type type)
    {
        // Exclude primitive types, strings, common system types
        if (type.IsPrimitive) return false;
        if (type == typeof(string)) return false;
        if (type == typeof(decimal)) return false;
        if (type == typeof(DateTime)) return false;
        if (type == typeof(DateTimeOffset)) return false;
        if (type == typeof(DateOnly)) return false;
        if (type == typeof(TimeOnly)) return false;
        if (type == typeof(Guid)) return false;
        if (type == typeof(Uri)) return false;
        if (type == typeof(object)) return false;
        if (type.IsEnum) return false;
        if (type.IsArray) return false;

        // Exclude generic collections
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(Dictionary<,>) ||
                genericDef == typeof(IDictionary<,>) ||
                genericDef == typeof(Nullable<>))
            {
                return false;
            }
        }

        // Must be a class (not interface, not abstract)
        if (!type.IsClass) return false;

        // Must have a parameterless constructor or be a record
        var hasParameterlessCtor = type.GetConstructor(Type.EmptyTypes) is not null;
        var isRecord = type.GetMethod("<Clone>$") is not null; // Records have a Clone method

        return hasParameterlessCtor || isRecord;
    }
}
