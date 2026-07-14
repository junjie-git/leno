using Consul;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Services;

public class ConsulServiceDiscoveryTests
{
    private static IOptions<ConsulOptions> DefaultOptions =>
        Microsoft.Extensions.Options.Options.Create(new ConsulOptions { Url = "http://localhost:8500", PassingOnly = true });

    [Fact]
    public async Task GetHealthyInstancesAsync_WithInstances_ReturnsMappedList()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var healthMock = new Mock<IHealthEndpoint>();

        var queryResult = new QueryResult<ServiceEntry[]>
        {
            Response = new[]
            {
                new ServiceEntry
                {
                    Service = new AgentService
                    {
                        ID = "product-1",
                        Address = "192.168.1.10",
                        Port = 8080,
                        Tags = new[] { "v1" }
                    }
                },
                new ServiceEntry
                {
                    Service = new AgentService
                    {
                        ID = "product-2",
                        Address = "192.168.1.11",
                        Port = 8080,
                        Tags = new[] { "v2" }
                    }
                }
            }
        };

        healthMock.Setup(h => h.Service("leno-product-api", null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);
        consulClientMock.SetupGet(c => c.Health).Returns(healthMock.Object);

        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        // Act
        var result = await discovery.GetHealthyInstancesAsync("leno-product-api", CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("product-1");
        result[0].Address.Should().Be("192.168.1.10");
        result[0].Port.Should().Be(8080);
        result[0].Tags.Should().Contain("v1");
        result[1].Id.Should().Be("product-2");
    }

    [Fact]
    public async Task GetHealthyInstancesAsync_WithNoInstances_ReturnsEmptyList()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var healthMock = new Mock<IHealthEndpoint>();

        healthMock.Setup(h => h.Service("leno-unknown", null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<ServiceEntry[]> { Response = Array.Empty<ServiceEntry>() });
        consulClientMock.SetupGet(c => c.Health).Returns(healthMock.Object);

        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        // Act
        var result = await discovery.GetHealthyInstancesAsync("leno-unknown", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHealthyInstancesAsync_WhenConsulThrows_ReturnsEmptyList()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var healthMock = new Mock<IHealthEndpoint>();

        healthMock.Setup(h => h.Service("leno-product-api", null, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        consulClientMock.SetupGet(c => c.Health).Returns(healthMock.Object);

        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        // Act
        var result = await discovery.GetHealthyInstancesAsync("leno-product-api", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHealthyInstancesAsync_WithNullServiceName_Throws()
    {
        var consulClientMock = new Mock<IConsulClient>();
        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        var act = async () => await discovery.GetHealthyInstancesAsync("", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetHealthyInstancesAsync_FiltersInstancesWithEmptyAddress()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var healthMock = new Mock<IHealthEndpoint>();

        var queryResult = new QueryResult<ServiceEntry[]>
        {
            Response = new[]
            {
                new ServiceEntry
                {
                    Service = new AgentService
                    {
                        ID = "valid-1",
                        Address = "192.168.1.10",
                        Port = 8080
                    }
                },
                new ServiceEntry
                {
                    Service = new AgentService
                    {
                        ID = "invalid-1",
                        Address = "",
                        Port = 8080
                    }
                }
            }
        };

        healthMock.Setup(h => h.Service("leno-product-api", null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);
        consulClientMock.SetupGet(c => c.Health).Returns(healthMock.Object);

        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        // Act
        var result = await discovery.GetHealthyInstancesAsync("leno-product-api", CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("valid-1");
    }
}
