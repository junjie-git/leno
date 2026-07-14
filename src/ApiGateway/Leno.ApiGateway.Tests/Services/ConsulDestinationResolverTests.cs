using Leno.ApiGateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Yarp.ReverseProxy.Configuration;

namespace Leno.ApiGateway.Tests.Services;

public class ConsulDestinationResolverTests
{
    private readonly Mock<IConsulServiceDiscovery> _discoveryMock;

    public ConsulDestinationResolverTests()
    {
        _discoveryMock = new Mock<IConsulServiceDiscovery>();
    }

    private void SetupDiscoveryInstances(string serviceName, params (string Id, string Address, int Port)[] instances)
    {
        var list = instances
            .Select(i => new ServiceInstance(i.Id, i.Address, i.Port, Array.Empty<string>()))
            .ToList();

        _discoveryMock.Setup(d => d.GetHealthyInstancesAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
    }

    private static IReadOnlyDictionary<string, DestinationConfig> CreateDestinations(
        string? consulServiceName = null,
        IReadOnlyDictionary<string, DestinationConfig>? staticDestinations = null)
    {
        if (consulServiceName is not null)
        {
            return new Dictionary<string, DestinationConfig>
            {
                ["consul"] = new DestinationConfig
                {
                    Address = "http://placeholder",
                    Metadata = new Dictionary<string, string> { ["ConsulServiceName"] = consulServiceName }
                }
            };
        }

        return staticDestinations ?? new Dictionary<string, DestinationConfig>();
    }

    [Fact]
    public async Task ResolveDestinationsAsync_WithConsulServiceName_ReturnsConsulInstances()
    {
        // Arrange
        SetupDiscoveryInstances("leno-product-api",
            ("product-1", "192.168.1.10", 8080),
            ("product-2", "192.168.1.11", 8080));

        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var destinations = CreateDestinations(consulServiceName: "leno-product-api");

        // Act
        var result = await resolver.ResolveDestinationsAsync(destinations, CancellationToken.None);

        // Assert
        result.Destinations.Should().HaveCount(2);
        result.Destinations.Values.Should().Contain(d => d.Address == "http://192.168.1.10:8080/");
        result.Destinations.Values.Should().Contain(d => d.Address == "http://192.168.1.11:8080/");
    }

    [Fact]
    public async Task ResolveDestinationsAsync_WithoutConsulServiceName_FallsBackToStaticDestinations()
    {
        // Arrange
        var staticDestinations = new Dictionary<string, DestinationConfig>
        {
            ["d1"] = new DestinationConfig { Address = "http://localhost:5150/" }
        };

        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var destinations = CreateDestinations(consulServiceName: null, staticDestinations: staticDestinations);

        // Act
        var result = await resolver.ResolveDestinationsAsync(destinations, CancellationToken.None);

        // Assert
        result.Destinations.Should().HaveCount(1);
        result.Destinations["d1"].Address.Should().Be("http://localhost:5150/");
        _discoveryMock.Verify(
            d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveDestinationsAsync_WithEmptyDestinations_ReturnsEmptyCollection()
    {
        // Arrange
        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var destinations = CreateDestinations(consulServiceName: null);

        // Act
        var result = await resolver.ResolveDestinationsAsync(destinations, CancellationToken.None);

        // Assert
        result.Destinations.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveDestinationsAsync_WhenNoHealthyInstances_ReturnsEmptyDestinations()
    {
        // Arrange
        SetupDiscoveryInstances("leno-product-api");

        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var destinations = CreateDestinations(consulServiceName: "leno-product-api");

        // Act
        var result = await resolver.ResolveDestinationsAsync(destinations, CancellationToken.None);

        // Assert
        result.Destinations.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveDestinationsAsync_WithConsulServiceName_DestinationIdsContainServiceName()
    {
        // Arrange
        SetupDiscoveryInstances("leno-product-api",
            ("instance-abc", "10.0.0.1", 8080));

        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var destinations = CreateDestinations(consulServiceName: "leno-product-api");

        // Act
        var result = await resolver.ResolveDestinationsAsync(destinations, CancellationToken.None);

        // Assert
        result.Destinations.Should().ContainKey("leno-product-api-instance-abc");
    }
}
