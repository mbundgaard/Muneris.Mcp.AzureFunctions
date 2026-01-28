using System.Text.Json;
using Muneris.Mcp.AzureFunctions.Attributes;
using Muneris.Mcp.AzureFunctions.Models.Resources;

namespace Muneris.Mcp.AzureFunctions.Sample.Resources;

/// <summary>
/// Sample MCP resources demonstrating the attribute-based definition pattern.
/// </summary>
public sealed class SampleResources
{
    /// <summary>
    /// Returns server configuration as a static resource.
    /// </summary>
    [McpResource("config://server", "Server Configuration", Description = "Current server configuration", AllowAnonymous = true)]
    public McpResourceResult GetServerConfig(ResourceRequestContext context)
    {
        var config = new
        {
            serverName = "Muneris MCP Sample",
            version = "1.0.0",
            protocol = context.ProtocolVersion ?? "2025-03-26",
            sessionId = context.SessionId,
            features = new[] { "tools", "resources" },
            timestamp = DateTime.UtcNow.ToString("O")
        };

        return new McpResourceResult
        {
            Contents = new[]
            {
                McpResourceContentItem.FromText(
                    context.Uri,
                    "application/json",
                    JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }))
            }
        };
    }

    /// <summary>
    /// Returns menu category items as a dynamic resource.
    /// </summary>
    [McpResource("menu://categories/{categoryId}", "Menu Category", Description = "Menu items in a specific category", AllowAnonymous = true)]
    public McpResourceResult GetMenuCategory(ResourceRequestContext context)
    {
        var categoryId = context.GetRequiredParameter("categoryId");

        // Sample menu data
        var categories = new Dictionary<string, object[]>
        {
            ["appetizers"] = new object[]
            {
                new { id = "app-1", name = "Spring Rolls", price = 8.99m },
                new { id = "app-2", name = "Soup of the Day", price = 6.99m },
                new { id = "app-3", name = "Bruschetta", price = 9.99m }
            },
            ["mains"] = new object[]
            {
                new { id = "main-1", name = "Grilled Salmon", price = 24.99m },
                new { id = "main-2", name = "Ribeye Steak", price = 32.99m },
                new { id = "main-3", name = "Pasta Primavera", price = 18.99m }
            },
            ["desserts"] = new object[]
            {
                new { id = "des-1", name = "Chocolate Cake", price = 7.99m },
                new { id = "des-2", name = "Ice Cream Sundae", price = 5.99m },
                new { id = "des-3", name = "Tiramisu", price = 8.99m }
            }
        };

        if (!categories.TryGetValue(categoryId.ToLowerInvariant(), out var items))
        {
            items = Array.Empty<object>();
        }

        var result = new
        {
            categoryId,
            items
        };

        return new McpResourceResult
        {
            Contents = new[]
            {
                McpResourceContentItem.FromText(
                    context.Uri,
                    "application/json",
                    JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }))
            }
        };
    }

    /// <summary>
    /// Dynamically lists available menu resources.
    /// </summary>
    [McpResourceList("menu")]
    public Task<IEnumerable<McpResourceInfo>> ListMenuResources(ResourceRequestContext context)
    {
        var resources = new List<McpResourceInfo>
        {
            new()
            {
                Uri = "menu://categories/appetizers",
                Name = "Appetizers",
                Description = "Starter dishes and small plates",
                MimeType = "application/json"
            },
            new()
            {
                Uri = "menu://categories/mains",
                Name = "Main Courses",
                Description = "Primary entrees and dishes",
                MimeType = "application/json"
            },
            new()
            {
                Uri = "menu://categories/desserts",
                Name = "Desserts",
                Description = "Sweet treats and desserts",
                MimeType = "application/json"
            }
        };

        return Task.FromResult<IEnumerable<McpResourceInfo>>(resources);
    }
}
