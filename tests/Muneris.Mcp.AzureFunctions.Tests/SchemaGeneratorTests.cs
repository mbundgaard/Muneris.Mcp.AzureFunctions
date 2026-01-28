using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Muneris.Mcp.AzureFunctions.Attributes;
using Muneris.Mcp.AzureFunctions.Services;
using Xunit;

namespace Muneris.Mcp.AzureFunctions.Tests;

public class SchemaGeneratorTests
{
    [Theory]
    [InlineData(typeof(string), "string")]
    [InlineData(typeof(int), "integer")]
    [InlineData(typeof(long), "integer")]
    [InlineData(typeof(short), "integer")]
    [InlineData(typeof(double), "number")]
    [InlineData(typeof(float), "number")]
    [InlineData(typeof(decimal), "number")]
    [InlineData(typeof(bool), "boolean")]
    public void GenerateSchema_ReturnsPrimitiveTypes(Type type, string expectedSchemaType)
    {
        var schema = SchemaGenerator.GenerateSchema(type);

        schema.Type.Should().Be(expectedSchemaType);
    }

    [Fact]
    public void GenerateSchema_HandlesNullableTypes()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(int?));

        schema.Type.Should().Be("integer");
    }

    [Fact]
    public void GenerateSchema_HandlesDateTime()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(DateTime));

        schema.Type.Should().Be("string");
        schema.Format.Should().Be("date-time");
    }

    [Fact]
    public void GenerateSchema_HandlesDateOnly()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(DateOnly));

        schema.Type.Should().Be("string");
        schema.Format.Should().Be("date");
    }

    [Fact]
    public void GenerateSchema_HandlesGuid()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(Guid));

        schema.Type.Should().Be("string");
        schema.Format.Should().Be("uuid");
    }

    [Fact]
    public void GenerateSchema_HandlesEnums()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(TestEnum));

        schema.Type.Should().Be("string");
        schema.Enum.Should().BeEquivalentTo("Low", "Medium", "High");
    }

    [Fact]
    public void GenerateSchema_HandlesArrays()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(string[]));

        schema.Type.Should().Be("array");
        schema.Items.Should().NotBeNull();
        schema.Items!.Type.Should().Be("string");
    }

    [Fact]
    public void GenerateSchema_HandlesLists()
    {
        var schema = SchemaGenerator.GenerateSchema(typeof(List<int>));

        schema.Type.Should().Be("array");
        schema.Items.Should().NotBeNull();
        schema.Items!.Type.Should().Be("integer");
    }

    [Fact]
    public void GenerateObjectSchema_GeneratesProperties()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(SimpleDto));

        schema.Type.Should().Be("object");
        schema.Properties.Should().ContainKey("name");
        schema.Properties.Should().ContainKey("age");
    }

    [Fact]
    public void GenerateObjectSchema_AppliesDescriptionAttribute()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(AnnotatedDto));

        schema.Properties!["name"].Description.Should().Be("The user's name");
    }

    [Fact]
    public void GenerateObjectSchema_AppliesRequiredAttribute()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(AnnotatedDto));

        schema.Required.Should().Contain("name");
    }

    [Fact]
    public void GenerateObjectSchema_AppliesRangeAttribute()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(AnnotatedDto));

        schema.Properties!["age"].Minimum.Should().Be(0);
        schema.Properties!["age"].Maximum.Should().Be(150);
    }

    [Fact]
    public void GenerateObjectSchema_AppliesStringLengthAttributes()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(AnnotatedDto));

        schema.Properties!["code"].MinLength.Should().Be(3);
        schema.Properties!["code"].MaxLength.Should().Be(10);
    }

    [Fact]
    public void GenerateObjectSchema_AppliesRegularExpressionAttribute()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(AnnotatedDto));

        schema.Properties!["postalCode"].Pattern.Should().Be(@"^\d{5}$");
    }

    [Fact]
    public void GenerateObjectSchema_AppliesMcpAllowedValuesAttribute()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(AnnotatedDto));

        schema.Properties!["priority"].Enum.Should().BeEquivalentTo("low", "normal", "high");
    }

    [Fact]
    public void GenerateObjectSchema_AppliesEmailAddressAttribute()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(AnnotatedDto));

        schema.Properties!["email"].Format.Should().Be("email");
    }

    [Fact]
    public void GenerateObjectSchema_AppliesUrlAttribute()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(AnnotatedDto));

        schema.Properties!["website"].Format.Should().Be("uri");
    }

    [Fact]
    public void GenerateObjectSchema_HandlesNestedObjects()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(NestedDto));

        schema.Properties!["address"].Type.Should().Be("object");
        schema.Properties!["address"].Properties.Should().ContainKey("street");
        schema.Properties!["address"].Properties.Should().ContainKey("city");
    }

    [Fact]
    public void GenerateObjectSchema_HandlesCollectionOfObjects()
    {
        var schema = SchemaGenerator.GenerateObjectSchema(typeof(ContainerDto));

        schema.Properties!["items"].Type.Should().Be("array");
        schema.Properties!["items"].Items!.Type.Should().Be("object");
        schema.Properties!["items"].Items!.Properties.Should().ContainKey("name");
    }

    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(DateTime), false)]
    [InlineData(typeof(List<string>), false)]
    [InlineData(typeof(string[]), false)]
    [InlineData(typeof(TestEnum), false)]
    [InlineData(typeof(SimpleDto), true)]
    [InlineData(typeof(AnnotatedDto), true)]
    public void IsPocoType_CorrectlyIdentifiesPocoTypes(Type type, bool expected)
    {
        SchemaGenerator.IsPocoType(type).Should().Be(expected);
    }

    private enum TestEnum
    {
        Low,
        Medium,
        High
    }

    private class SimpleDto
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private class AnnotatedDto
    {
        [Description("The user's name")]
        [Required]
        public string Name { get; set; } = string.Empty;

        [Range(0, 150)]
        public int Age { get; set; }

        [StringLength(10, MinimumLength = 3)]
        public string Code { get; set; } = string.Empty;

        [RegularExpression(@"^\d{5}$")]
        public string PostalCode { get; set; } = string.Empty;

        [McpAllowedValues("low", "normal", "high")]
        public string Priority { get; set; } = "normal";

        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Url]
        public string Website { get; set; } = string.Empty;
    }

    private class NestedDto
    {
        public string Name { get; set; } = string.Empty;
        public AddressDto Address { get; set; } = new();
    }

    private class AddressDto
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    private class ContainerDto
    {
        public string ContainerName { get; set; } = string.Empty;
        public List<ItemDto> Items { get; set; } = new();
    }

    private class ItemDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
