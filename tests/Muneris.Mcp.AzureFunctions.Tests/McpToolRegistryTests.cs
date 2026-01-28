using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muneris.Mcp.AzureFunctions.Attributes;
using Muneris.Mcp.AzureFunctions.Models;
using Muneris.Mcp.AzureFunctions.Services;
using Xunit;

namespace Muneris.Mcp.AzureFunctions.Tests;

public class McpToolRegistryTests
{
    private readonly McpToolRegistry _registry;
    private readonly IServiceProvider _serviceProvider;

    public McpToolRegistryTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<McpToolRegistry>>();
        _registry = new McpToolRegistry(_serviceProvider, logger);
    }

    [Fact]
    public void RegisterToolsFromType_RegistersToolsWithAttribute()
    {
        _registry.RegisterToolsFromType(typeof(TestTools));

        var tools = _registry.GetToolDefinitions();

        tools.Should().ContainSingle(t => t.Name == "test_tool");
    }

    [Fact]
    public void RegisterToolsFromType_IncludesPropertiesInSchema()
    {
        _registry.RegisterToolsFromType(typeof(TestTools));

        var tools = _registry.GetToolDefinitions();
        var tool = tools.First(t => t.Name == "test_tool");

        tool.InputSchema.Properties.Should().ContainKey("message");
        tool.InputSchema.Properties["message"].Type.Should().Be("string");
        tool.InputSchema.Properties["message"].Description.Should().Be("Test message");
    }

    [Fact]
    public void RegisterToolsFromType_MarksRequiredProperties()
    {
        _registry.RegisterToolsFromType(typeof(TestTools));

        var tools = _registry.GetToolDefinitions();
        var tool = tools.First(t => t.Name == "test_tool");

        tool.InputSchema.Required.Should().Contain("message");
    }

    [Fact]
    public void HasTool_ReturnsTrueForRegisteredTool()
    {
        _registry.RegisterToolsFromType(typeof(TestTools));

        _registry.HasTool("test_tool").Should().BeTrue();
    }

    [Fact]
    public void HasTool_ReturnsFalseForUnregisteredTool()
    {
        _registry.HasTool("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void IsToolAnonymous_ReturnsTrueForAnonymousTool()
    {
        _registry.RegisterToolsFromType(typeof(TestTools));

        _registry.IsToolAnonymous("test_tool").Should().BeTrue();
    }

    [Fact]
    public void IsToolAnonymous_ReturnsFalseForAuthenticatedTool()
    {
        _registry.RegisterToolsFromType(typeof(TestTools));

        _registry.IsToolAnonymous("auth_tool").Should().BeFalse();
    }

    [Fact]
    public void RegisterToolsFromType_IncludesAnnotations()
    {
        _registry.RegisterToolsFromType(typeof(AnnotatedTools));

        var tools = _registry.GetToolDefinitions();
        var tool = tools.First(t => t.Name == "annotated_tool");

        tool.Annotations.Should().NotBeNull();
        tool.Annotations!.Title.Should().Be("Annotated Tool");
        tool.Annotations.ReadOnlyHint.Should().BeTrue();
        tool.Annotations.IdempotentHint.Should().BeTrue();
        tool.Annotations.OpenWorldHint.Should().BeFalse();
    }

    [Fact]
    public void RegisterToolsFromType_OmitsAnnotationsWhenDefaults()
    {
        _registry.RegisterToolsFromType(typeof(TestTools));

        var tools = _registry.GetToolDefinitions();
        var tool = tools.First(t => t.Name == "test_tool");

        // Annotations should be null when all values are defaults
        tool.Annotations.Should().BeNull();
    }

    [Fact]
    public void RegisterToolsFromType_IncludesEnhancedSchemaProperties()
    {
        _registry.RegisterToolsFromType(typeof(EnhancedSchemaTools));

        var tools = _registry.GetToolDefinitions();
        var tool = tools.First(t => t.Name == "enhanced_schema_tool");

        var props = tool.InputSchema.Properties;

        props["priority"].Enum.Should().BeEquivalentTo("low", "normal", "high");
        props["email"].Format.Should().Be("email");
        props["count"].Minimum.Should().Be(1);
        props["count"].Maximum.Should().Be(100);
        props["code"].Pattern.Should().Be("^[A-Z]{3}$");
        props["name"].MinLength.Should().Be(1);
        props["name"].MaxLength.Should().Be(50);
    }

    [Fact]
    public void RegisterToolsFromType_GeneratesSchemaFromPoco()
    {
        _registry.RegisterToolsFromType(typeof(PocoTools));

        var tools = _registry.GetToolDefinitions();
        var tool = tools.First(t => t.Name == "poco_tool");

        var props = tool.InputSchema.Properties;

        props.Should().ContainKey("customerId");
        props["customerId"].Type.Should().Be("string");
        props["customerId"].Description.Should().Be("Customer ID");

        props.Should().ContainKey("amount");
        props["amount"].Type.Should().Be("number");
        props["amount"].Minimum.Should().Be(0);
        props["amount"].Maximum.Should().Be(10000);

        tool.InputSchema.Required.Should().Contain("customerId");
        tool.InputSchema.Required.Should().Contain("amount");
    }

    [Fact]
    public void RegisterToolsFromType_GeneratesNestedObjectSchema()
    {
        _registry.RegisterToolsFromType(typeof(NestedPocoTools));

        var tools = _registry.GetToolDefinitions();
        var tool = tools.First(t => t.Name == "nested_poco_tool");

        var props = tool.InputSchema.Properties;

        props.Should().ContainKey("items");
        props["items"].Type.Should().Be("array");
        props["items"].Items.Should().NotBeNull();
        props["items"].Items!.Type.Should().Be("object");
        props["items"].Items!.Properties.Should().ContainKey("productId");
        props["items"].Items!.Properties.Should().ContainKey("quantity");
    }

    private class TestTools
    {
        [McpTool("test_tool", Description = "A test tool", AllowAnonymous = true)]
        [McpToolProperty("message", Type = "string", Description = "Test message", Required = true)]
        public string TestTool(string message) => message;

        [McpTool("auth_tool", Description = "A tool requiring authentication")]
        public string AuthTool(ToolInvocationContext context) => "authenticated";
    }

    private class AnnotatedTools
    {
        [McpTool("annotated_tool",
            Description = "A tool with annotations",
            AllowAnonymous = true,
            Title = "Annotated Tool",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false)]
        public string AnnotatedTool() => "result";
    }

    private class EnhancedSchemaTools
    {
        [McpTool("enhanced_schema_tool", Description = "Tool with enhanced schema", AllowAnonymous = true)]
        [McpToolProperty("priority", Type = "string", Description = "Priority level", Enum = new[] { "low", "normal", "high" })]
        [McpToolProperty("email", Type = "string", Description = "Email address", Format = "email")]
        [McpToolProperty("count", Type = "integer", Description = "Count", Minimum = 1, Maximum = 100)]
        [McpToolProperty("code", Type = "string", Description = "Code", Pattern = "^[A-Z]{3}$")]
        [McpToolProperty("name", Type = "string", Description = "Name", MinLength = 1, MaxLength = 50)]
        public string EnhancedSchemaTool() => "result";
    }

    private class PocoTools
    {
        [McpTool("poco_tool", Description = "Tool with POCO binding", AllowAnonymous = true)]
        public string PocoTool(ToolInvocationContext context, PocoRequest request) => "result";
    }

    private class PocoRequest
    {
        [Description("Customer ID")]
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [Description("Amount")]
        [Required]
        [Range(0, 10000)]
        public decimal Amount { get; set; }

        [Description("Notes")]
        public string? Notes { get; set; }
    }

    private class NestedPocoTools
    {
        [McpTool("nested_poco_tool", Description = "Tool with nested POCO", AllowAnonymous = true)]
        public string NestedPocoTool(ToolInvocationContext context, NestedPocoRequest request) => "result";
    }

    private class NestedPocoRequest
    {
        [Description("Order ID")]
        [Required]
        public string OrderId { get; set; } = string.Empty;

        [Description("Order items")]
        [Required]
        public List<OrderItemDto> Items { get; set; } = new();
    }

    private class OrderItemDto
    {
        [Description("Product ID")]
        [Required]
        public string ProductId { get; set; } = string.Empty;

        [Description("Quantity")]
        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }
    }
}
