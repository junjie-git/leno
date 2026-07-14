using System.Net.Http;
using Leno.ApiGateway.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.ApiGateway.Tests.Transforms;

public class ProtocolTranslatorRegistryTests
{
    private sealed class TestHttpToGrpcTranslator : IProtocolTranslator
    {
        public string SourceProtocol => "HTTP";
        public string TargetProtocol => "gRPC";
        public Task<HttpRequestMessage> TranslateRequestAsync(HttpContext context)
            => Task.FromResult(new HttpRequestMessage());
        public Task TranslateResponseAsync(HttpContext context, HttpResponseMessage response)
            => Task.CompletedTask;
    }

    private sealed class TestGrpcToHttpTranslator : IProtocolTranslator
    {
        public string SourceProtocol => "gRPC";
        public string TargetProtocol => "HTTP";
        public Task<HttpRequestMessage> TranslateRequestAsync(HttpContext context)
            => Task.FromResult(new HttpRequestMessage());
        public Task TranslateResponseAsync(HttpContext context, HttpResponseMessage response)
            => Task.CompletedTask;
    }

    [Fact]
    public void Find_WithRegisteredTranslator_ReturnsTranslator()
    {
        // Arrange
        var translators = new IProtocolTranslator[]
        {
            new TestHttpToGrpcTranslator(),
            new TestGrpcToHttpTranslator()
        };
        var registry = new ProtocolTranslatorRegistry(
            translators, NullLogger<ProtocolTranslatorRegistry>.Instance);

        // Act
        var result = registry.Find("HTTP", "gRPC");

        // Assert
        result.Should().NotBeNull();
        result!.SourceProtocol.Should().Be("HTTP");
        result.TargetProtocol.Should().Be("gRPC");
    }

    [Fact]
    public void Find_WithUnregisteredPair_ReturnsNull()
    {
        // Arrange
        var translators = new IProtocolTranslator[] { new TestHttpToGrpcTranslator() };
        var registry = new ProtocolTranslatorRegistry(
            translators, NullLogger<ProtocolTranslatorRegistry>.Instance);

        // Act
        var result = registry.Find("HTTP", "WebSocket");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        // Arrange
        var translators = new IProtocolTranslator[] { new TestHttpToGrpcTranslator() };
        var registry = new ProtocolTranslatorRegistry(
            translators, NullLogger<ProtocolTranslatorRegistry>.Instance);

        // Act
        var result = registry.Find("http", "GRPC");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Find_WithEmptyProtocols_ReturnsNull()
    {
        // Arrange
        var registry = new ProtocolTranslatorRegistry(
            Array.Empty<IProtocolTranslator>(),
            NullLogger<ProtocolTranslatorRegistry>.Instance);

        // Act
        var result = registry.Find("HTTP", "gRPC");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void All_ContainsAllRegisteredTranslators()
    {
        // Arrange
        var t1 = new TestHttpToGrpcTranslator();
        var t2 = new TestGrpcToHttpTranslator();
        var registry = new ProtocolTranslatorRegistry(
            new IProtocolTranslator[] { t1, t2 },
            NullLogger<ProtocolTranslatorRegistry>.Instance);

        // Act
        var all = registry.All;

        // Assert
        all.Should().HaveCount(2);
        all.Should().Contain(t1);
        all.Should().Contain(t2);
    }

    [Fact]
    public void Constructor_WithDuplicatePair_LastOneWins()
    {
        // Arrange
        var first = new TestHttpToGrpcTranslator();
        var second = new TestHttpToGrpcTranslator();
        var registry = new ProtocolTranslatorRegistry(
            new IProtocolTranslator[] { first, second },
            NullLogger<ProtocolTranslatorRegistry>.Instance);

        // Act
        var result = registry.Find("HTTP", "gRPC");

        // Assert — 后注册的覆盖先注册的
        result.Should().BeSameAs(second);
    }
}
