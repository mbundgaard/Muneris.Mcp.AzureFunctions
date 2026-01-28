using System.Text.Json;
using FluentAssertions;
using Muneris.Mcp.AzureFunctions.Models;
using Xunit;

namespace Muneris.Mcp.AzureFunctions.Tests;

public class JsonRpcModelsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void JsonRpcRequest_DeserializesCorrectly()
    {
        var json = """{"jsonrpc":"2.0","id":1,"method":"test","params":{"foo":"bar"}}""";

        var request = JsonSerializer.Deserialize<JsonRpcRequest>(json, JsonOptions);

        request.Should().NotBeNull();
        request!.JsonRpc.Should().Be("2.0");
        request.Method.Should().Be("test");
        request.Id.Should().NotBeNull();
    }

    [Fact]
    public void JsonRpcResponse_Success_SerializesCorrectly()
    {
        var response = JsonRpcResponse.Success(
            JsonDocument.Parse("1").RootElement,
            new { message = "hello" });

        var json = JsonSerializer.Serialize(response, JsonOptions);

        json.Should().Contain("\"jsonrpc\":\"2.0\"");
        json.Should().Contain("\"result\"");
        json.Should().Contain("\"message\":\"hello\"");
        json.Should().NotContain("\"error\"");
    }

    [Fact]
    public void JsonRpcResponse_Failure_SerializesCorrectly()
    {
        var response = JsonRpcResponse.Failure(
            JsonDocument.Parse("1").RootElement,
            JsonRpcErrorCodes.MethodNotFound,
            "Method not found");

        var json = JsonSerializer.Serialize(response, JsonOptions);

        json.Should().Contain("\"jsonrpc\":\"2.0\"");
        json.Should().Contain("\"error\"");
        json.Should().Contain("-32601");
        json.Should().Contain("\"Method not found\"");
        json.Should().NotContain("\"result\"");
    }

    [Fact]
    public void JsonRpcErrorCodes_HaveCorrectValues()
    {
        JsonRpcErrorCodes.ParseError.Should().Be(-32700);
        JsonRpcErrorCodes.InvalidRequest.Should().Be(-32600);
        JsonRpcErrorCodes.MethodNotFound.Should().Be(-32601);
        JsonRpcErrorCodes.InvalidParams.Should().Be(-32602);
        JsonRpcErrorCodes.InternalError.Should().Be(-32603);
        JsonRpcErrorCodes.AuthenticationError.Should().Be(-32001);
        JsonRpcErrorCodes.ResourceNotFound.Should().Be(-32002);
    }
}
