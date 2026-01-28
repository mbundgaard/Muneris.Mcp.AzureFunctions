using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muneris.Mcp.AzureFunctions.Attributes;
using Muneris.Mcp.AzureFunctions.Models.Resources;
using Muneris.Mcp.AzureFunctions.Services;
using Xunit;

namespace Muneris.Mcp.AzureFunctions.Tests;

public class McpResourceRegistryTests
{
    private readonly McpResourceRegistry _registry;
    private readonly IServiceProvider _serviceProvider;

    public McpResourceRegistryTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<McpResourceRegistry>>();
        _registry = new McpResourceRegistry(_serviceProvider, logger);
    }

    [Fact]
    public void RegisterResourcesFromType_RegistersStaticResources()
    {
        _registry.RegisterResourcesFromType(typeof(TestResources));

        var resources = _registry.GetStaticResourceDefinitions();

        resources.Should().ContainSingle(r => r.Uri == "config://server");
    }

    [Fact]
    public void RegisterResourcesFromType_IncludesResourceMetadata()
    {
        _registry.RegisterResourcesFromType(typeof(TestResources));

        var resources = _registry.GetStaticResourceDefinitions();
        var resource = resources.First(r => r.Uri == "config://server");

        resource.Name.Should().Be("Server Config");
        resource.Description.Should().Be("Server configuration");
        resource.MimeType.Should().Be("application/json");
    }

    [Fact]
    public void HasResource_ReturnsTrueForStaticUri()
    {
        _registry.RegisterResourcesFromType(typeof(TestResources));

        _registry.HasResource("config://server").Should().BeTrue();
    }

    [Fact]
    public void HasResource_ReturnsTrueForParameterizedUri()
    {
        _registry.RegisterResourcesFromType(typeof(TestResources));

        _registry.HasResource("items://categories/appetizers").Should().BeTrue();
        _registry.HasResource("items://categories/desserts").Should().BeTrue();
    }

    [Fact]
    public void HasResource_ReturnsFalseForUnregisteredUri()
    {
        _registry.RegisterResourcesFromType(typeof(TestResources));

        _registry.HasResource("unknown://resource").Should().BeFalse();
    }

    [Fact]
    public void IsResourceAnonymous_ReturnsTrueForAnonymousResource()
    {
        _registry.RegisterResourcesFromType(typeof(TestResources));

        _registry.IsResourceAnonymous("config://server").Should().BeTrue();
    }

    [Fact]
    public void IsResourceAnonymous_ReturnsFalseForAuthenticatedResource()
    {
        _registry.RegisterResourcesFromType(typeof(TestResources));

        _registry.IsResourceAnonymous("secure://data").Should().BeFalse();
    }

    [Fact]
    public void RegisterResourcesFromType_RegistersResourceListProviders()
    {
        _registry.RegisterResourcesFromType(typeof(ResourceListProviders));

        _registry.HasAnyResources.Should().BeTrue();
    }

    [Fact]
    public void HasAnyResources_ReturnsFalseWhenEmpty()
    {
        _registry.HasAnyResources.Should().BeFalse();
    }

    [Fact]
    public void HasAnyResources_ReturnsTrueWhenResourcesRegistered()
    {
        _registry.RegisterResourcesFromType(typeof(TestResources));

        _registry.HasAnyResources.Should().BeTrue();
    }

    private class TestResources
    {
        [McpResource("config://server", "Server Config", Description = "Server configuration", AllowAnonymous = true)]
        public McpResourceResult GetServerConfig(ResourceRequestContext context)
        {
            return new McpResourceResult
            {
                Contents = new[] { McpResourceContentItem.FromText(context.Uri, "application/json", "{}") }
            };
        }

        [McpResource("items://categories/{categoryId}", "Category Items", Description = "Items in a category", AllowAnonymous = true)]
        public McpResourceResult GetCategoryItems(ResourceRequestContext context)
        {
            return new McpResourceResult
            {
                Contents = new[] { McpResourceContentItem.FromText(context.Uri, "application/json", "[]") }
            };
        }

        [McpResource("secure://data", "Secure Data", Description = "Protected data")]
        public McpResourceResult GetSecureData(ResourceRequestContext context)
        {
            return new McpResourceResult
            {
                Contents = new[] { McpResourceContentItem.FromText(context.Uri, "application/json", "{}") }
            };
        }
    }

    private class ResourceListProviders
    {
        [McpResourceList("dynamic")]
        public Task<IEnumerable<McpResourceInfo>> ListDynamicResources(ResourceRequestContext context)
        {
            return Task.FromResult<IEnumerable<McpResourceInfo>>(new List<McpResourceInfo>
            {
                new() { Uri = "dynamic://item1", Name = "Item 1" },
                new() { Uri = "dynamic://item2", Name = "Item 2" }
            });
        }
    }
}
