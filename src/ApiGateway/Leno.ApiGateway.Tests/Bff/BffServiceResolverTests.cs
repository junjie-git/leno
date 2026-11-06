using FluentAssertions;
using Leno.ApiGateway.Bff;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.ApiGateway.Tests.Bff;

/// <summary>
/// <see cref="BffServiceResolver"/> 单元测试：覆盖清单名→Consul 名解析、显式 BaseUrl 优先与全部失败路径。
/// </summary>
public sealed class BffServiceResolverTests
{
    private static BffServiceResolver CreateResolver(
        BffOptions options,
        IConsulServiceDiscovery discovery)
    {
        return new BffServiceResolver(
            new TestOptionsMonitor(options),
            discovery,
            NullLogger<BffServiceResolver>.Instance);
    }

    [Fact]
    public async Task ResolveBaseAddressAsync_ManifestName_MapsToConsulServiceName()
    {
        // Arrange：Bff:Services:product → Consul 名 leno-product-api
        var options = new BffOptions
        {
            Services = new Dictionary<string, BffServiceEndpoint>(StringComparer.OrdinalIgnoreCase)
            {
                ["product"] = new() { ConsulServiceName = "leno-product-api" }
            }
        };

        var discovery = new Mock<IConsulServiceDiscovery>();
        discovery
            .Setup(d => d.GetHealthyInstancesAsync("leno-product-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ServiceInstance("product-1", "10.0.0.5", 5152, Array.Empty<string>())
            });

        var resolver = CreateResolver(options, discovery.Object);
        var ct = CancellationToken.None;

        // Act
        var baseAddress = await resolver.ResolveBaseAddressAsync("product", ct);

        // Assert
        baseAddress.Should().Be("http://10.0.0.5:5152");
        discovery.Verify(
            d => d.GetHealthyInstancesAsync("leno-product-api", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveBaseAddressAsync_ExplicitBaseUrl_TakesPrecedenceOverConsul()
    {
        // Arrange：BaseUrl 与 ConsulServiceName 同时配置，显式基址必须优先且不触发服务发现
        var options = new BffOptions
        {
            Services = new Dictionary<string, BffServiceEndpoint>(StringComparer.OrdinalIgnoreCase)
            {
                ["product"] = new()
                {
                    BaseUrl = "http://explicit-product:5152/",
                    ConsulServiceName = "leno-product-api"
                }
            }
        };

        var discovery = new Mock<IConsulServiceDiscovery>(MockBehavior.Strict);
        var resolver = CreateResolver(options, discovery.Object);

        // Act
        var baseAddress = await resolver.ResolveBaseAddressAsync("product", CancellationToken.None);

        // Assert：尾部斜杠被去掉，且完全没有访问 Consul
        baseAddress.Should().Be("http://explicit-product:5152");
        discovery.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveBaseAddressAsync_ServiceNotRegistered_ThrowsServiceUnavailable()
    {
        var options = new BffOptions();
        var discovery = new Mock<IConsulServiceDiscovery>();
        var resolver = CreateResolver(options, discovery.Object);

        var act = async () => await resolver.ResolveBaseAddressAsync("unknown-service", CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<BffServiceUnavailableException>();
        assertion.Which.ServiceName.Should().Be("unknown-service");
        assertion.Which.Message.Should().Contain("未在 Bff:Services 中登记");
    }

    [Fact]
    public async Task ResolveBaseAddressAsync_NoHealthyConsulInstance_ThrowsServiceUnavailable()
    {
        var options = new BffOptions
        {
            Services = new Dictionary<string, BffServiceEndpoint>(StringComparer.OrdinalIgnoreCase)
            {
                ["review"] = new() { ConsulServiceName = "leno-review-api" }
            }
        };

        var discovery = new Mock<IConsulServiceDiscovery>();
        discovery
            .Setup(d => d.GetHealthyInstancesAsync("leno-review-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        var resolver = CreateResolver(options, discovery.Object);

        var act = async () => await resolver.ResolveBaseAddressAsync("review", CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<BffServiceUnavailableException>();
        assertion.Which.ServiceName.Should().Be("review");
        assertion.Which.ConsulServiceName.Should().Be("leno-review-api");
        assertion.Which.Message.Should().Contain("无健康实例");
    }

    [Fact]
    public async Task ResolveBaseAddressAsync_InvalidExplicitBaseUrl_ThrowsServiceUnavailable()
    {
        var options = new BffOptions
        {
            Services = new Dictionary<string, BffServiceEndpoint>(StringComparer.OrdinalIgnoreCase)
            {
                ["order"] = new() { BaseUrl = "ftp://invalid-host:5154", ConsulServiceName = "leno-order-api" }
            }
        };

        var discovery = new Mock<IConsulServiceDiscovery>(MockBehavior.Strict);
        var resolver = CreateResolver(options, discovery.Object);

        var act = async () => await resolver.ResolveBaseAddressAsync("order", CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<BffServiceUnavailableException>();
        assertion.Which.ServiceName.Should().Be("order");
        assertion.Which.Message.Should().Contain("合法的 http/https 绝对地址");
        discovery.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveBaseAddressAsync_NeitherBaseUrlNorConsulName_ThrowsServiceUnavailable()
    {
        var options = new BffOptions
        {
            Services = new Dictionary<string, BffServiceEndpoint>(StringComparer.OrdinalIgnoreCase)
            {
                ["cart"] = new()
            }
        };

        var discovery = new Mock<IConsulServiceDiscovery>(MockBehavior.Strict);
        var resolver = CreateResolver(options, discovery.Object);

        var act = async () => await resolver.ResolveBaseAddressAsync("cart", CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<BffServiceUnavailableException>();
        assertion.Which.Message.Should().Contain("既未配置 BaseUrl 也未配置 ConsulServiceName");
        discovery.VerifyNoOtherCalls();
    }

    /// <summary>
    /// 极简 <see cref="IOptionsMonitor{T}"/> 桩：CurrentValue 固定，OnChange 不触发。
    /// </summary>
    private sealed class TestOptionsMonitor : IOptionsMonitor<BffOptions>
    {
        public TestOptionsMonitor(BffOptions value)
        {
            CurrentValue = value;
        }

        public BffOptions CurrentValue { get; }

        public BffOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<BffOptions, string?> listener) => null;
    }
}
