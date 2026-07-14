using Consul;
using Leno.Infrastructure.ServiceDiscovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Infrastructure.Tests.ServiceDiscovery;

public class ConsulServiceRegistrationExtensionsTests
{
    [Fact]
    public void AddConsulServiceRegistration_RegistersConsulClientAndHostedService()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder([]);

        // Act
        builder.AddConsulServiceRegistration("leno-product-api", opts =>
        {
            opts.Address = "192.168.1.10";
            opts.Port = 8080;
        });

        using var host = builder.Build();

        // Assert
        host.Services.GetService<IConsulClient>().Should().NotBeNull();
        host.Services.GetServices<IHostedService>()
            .Should().Contain(s => s is ConsulServiceRegistrationHostedService);
        host.Services.GetService<ConsulRegistrationOptions>().Should().NotBeNull();
    }

    [Fact]
    public void AddConsulServiceRegistration_NullBuilder_Throws()
    {
        IHostApplicationBuilder builder = null!;

        var act = () => builder.AddConsulServiceRegistration("test-service");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddConsulServiceRegistration_NullServiceName_Throws()
    {
        var builder = Host.CreateApplicationBuilder([]);

        var act = () => builder.AddConsulServiceRegistration(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public class ConsulServiceRegistrationHostedServiceTests
{
    [Fact]
    public async Task StartAsync_RegistersServiceWithConsul()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var agentMock = new Mock<IAgentEndpoint>();

        consulClientMock.SetupGet(c => c.Agent).Returns(agentMock.Object);

        var options = new ConsulRegistrationOptions
        {
            ServiceName = "leno-product-api",
            ServiceId = "leno-product-api-instance-1",
            Address = "192.168.1.10",
            Port = 8080,
            HealthCheckPath = "/health/live"
        };

        var service = new ConsulServiceRegistrationHostedService(
            consulClientMock.Object, options,
            NullLogger<ConsulServiceRegistrationHostedService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        agentMock.Verify(a => a.ServiceRegister(
            It.Is<AgentServiceRegistration>(r =>
                r.ID == "leno-product-api-instance-1" &&
                r.Name == "leno-product-api" &&
                r.Address == "192.168.1.10" &&
                r.Port == 8080 &&
                r.Check != null &&
                r.Check.HTTP == "http://192.168.1.10:8080/health/live"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_DeregistersServiceFromConsul()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var agentMock = new Mock<IAgentEndpoint>();

        consulClientMock.SetupGet(c => c.Agent).Returns(agentMock.Object);

        var options = new ConsulRegistrationOptions
        {
            ServiceName = "leno-product-api",
            ServiceId = "leno-product-api-instance-1",
            Address = "192.168.1.10",
            Port = 8080
        };

        var service = new ConsulServiceRegistrationHostedService(
            consulClientMock.Object, options,
            NullLogger<ConsulServiceRegistrationHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        agentMock.Verify(a => a.ServiceDeregister(
            "leno-product-api-instance-1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_RegistersTagsWhenProvided()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var agentMock = new Mock<IAgentEndpoint>();

        consulClientMock.SetupGet(c => c.Agent).Returns(agentMock.Object);

        var options = new ConsulRegistrationOptions
        {
            ServiceName = "leno-product-api",
            ServiceId = "leno-product-api-1",
            Address = "10.0.0.1",
            Port = 8080,
            Tags = new[] { "v1", "primary" }
        };

        var service = new ConsulServiceRegistrationHostedService(
            consulClientMock.Object, options,
            NullLogger<ConsulServiceRegistrationHostedService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        agentMock.Verify(a => a.ServiceRegister(
            It.Is<AgentServiceRegistration>(r =>
                r.Tags != null &&
                r.Tags.Contains("v1") &&
                r.Tags.Contains("primary")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
