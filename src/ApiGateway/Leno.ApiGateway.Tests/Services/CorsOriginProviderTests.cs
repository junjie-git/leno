using System.Text;
using Consul;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Services;

public class CorsOriginProviderTests
{
    private static CorsOptions DefaultOptions => new()
    {
        Enabled = true,
        AllowedOrigins = new[] { "https://default.leno.com" },
        ConsulKvKey = "leno/gateway/cors-origins"
    };

    [Fact]
    public void IsOriginAllowed_WithConfiguredOrigin_ReturnsTrue()
    {
        // Arrange
        var consulMock = new Mock<IConsulClient>();
        var provider = new ConsulCorsOriginProvider(
            consulMock.Object, Microsoft.Extensions.Options.Options.Create(DefaultOptions),
            NullLogger<ConsulCorsOriginProvider>.Instance);

        // Act & Assert
        provider.IsOriginAllowed("https://default.leno.com").Should().BeTrue();
    }

    [Fact]
    public void IsOriginAllowed_WithUnknownOrigin_ReturnsFalse()
    {
        // Arrange
        var consulMock = new Mock<IConsulClient>();
        var provider = new ConsulCorsOriginProvider(
            consulMock.Object, Microsoft.Extensions.Options.Options.Create(DefaultOptions),
            NullLogger<ConsulCorsOriginProvider>.Instance);

        // Act & Assert
        provider.IsOriginAllowed("https://evil.example.com").Should().BeFalse();
    }

    [Fact]
    public void IsOriginAllowed_IsCaseInsensitive()
    {
        // Arrange
        var consulMock = new Mock<IConsulClient>();
        var provider = new ConsulCorsOriginProvider(
            consulMock.Object, Microsoft.Extensions.Options.Options.Create(DefaultOptions),
            NullLogger<ConsulCorsOriginProvider>.Instance);

        // Act & Assert
        provider.IsOriginAllowed("HTTPS://DEFAULT.LENO.COM").Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_LoadsOriginsFromConsulKV()
    {
        // Arrange
        var consulMock = new Mock<IConsulClient>();
        var kvMock = new Mock<IKVEndpoint>();

        var json = "[\"https://leno.example.com\",\"https://admin.leno.com\"]";
        var kvPair = new KVPair("leno/gateway/cors-origins")
        {
            Value = Encoding.UTF8.GetBytes(json)
        };

        kvMock.Setup(k => k.Get("leno/gateway/cors-origins", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<KVPair> { Response = kvPair });
        consulMock.SetupGet(c => c.KV).Returns(kvMock.Object);

        var provider = new ConsulCorsOriginProvider(
            consulMock.Object, Microsoft.Extensions.Options.Options.Create(DefaultOptions),
            NullLogger<ConsulCorsOriginProvider>.Instance);

        // Act
        await provider.RefreshAsync(CancellationToken.None);

        // Assert
        provider.IsOriginAllowed("https://leno.example.com").Should().BeTrue();
        provider.IsOriginAllowed("https://admin.leno.com").Should().BeTrue();
        provider.IsOriginAllowed("https://default.leno.com").Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_WhenConsulReturnsNull_KeepsExistingOrigins()
    {
        // Arrange
        var consulMock = new Mock<IConsulClient>();
        var kvMock = new Mock<IKVEndpoint>();

        kvMock.Setup(k => k.Get("leno/gateway/cors-origins", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<KVPair> { Response = null! });
        consulMock.SetupGet(c => c.KV).Returns(kvMock.Object);

        var provider = new ConsulCorsOriginProvider(
            consulMock.Object, Microsoft.Extensions.Options.Options.Create(DefaultOptions),
            NullLogger<ConsulCorsOriginProvider>.Instance);

        // Act
        await provider.RefreshAsync(CancellationToken.None);

        // Assert — 配置中的默认 Origin 仍然有效
        provider.IsOriginAllowed("https://default.leno.com").Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_WhenConsulThrows_LogsAndKeepsExistingOrigins()
    {
        // Arrange
        var consulMock = new Mock<IConsulClient>();
        var kvMock = new Mock<IKVEndpoint>();

        kvMock.Setup(k => k.Get("leno/gateway/cors-origins", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Consul unavailable"));
        consulMock.SetupGet(c => c.KV).Returns(kvMock.Object);

        var provider = new ConsulCorsOriginProvider(
            consulMock.Object, Microsoft.Extensions.Options.Options.Create(DefaultOptions),
            NullLogger<ConsulCorsOriginProvider>.Instance);

        // Act
        await provider.RefreshAsync(CancellationToken.None);

        // Assert — 不抛出异常，保留默认配置
        provider.IsOriginAllowed("https://default.leno.com").Should().BeTrue();
    }

    [Fact]
    public void AllowedOrigins_AfterConstruction_ContainsConfiguredOrigins()
    {
        // Arrange
        var consulMock = new Mock<IConsulClient>();
        var provider = new ConsulCorsOriginProvider(
            consulMock.Object, Microsoft.Extensions.Options.Options.Create(DefaultOptions),
            NullLogger<ConsulCorsOriginProvider>.Instance);

        // Act
        var origins = provider.AllowedOrigins;

        // Assert
        origins.Should().Contain("https://default.leno.com");
    }
}
