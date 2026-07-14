using Consul;
using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leno.ApiGateway.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Consul:Url"] = "http://localhost:8500",
                ["Consul:Token"] = "test-token"
            })
            .Build();

    [Fact]
    public void AddConsulServiceDiscovery_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateConfig();

        // Act
        services.AddConsulServiceDiscovery(config);

        // Assert
        services.Should().Contain(s => s.ServiceType == typeof(IConsulClient));
        services.Should().Contain(s => s.ServiceType == typeof(IConsulServiceDiscovery));
    }

    [Fact]
    public void AddConsulServiceDiscovery_BindsConsulOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateConfig();

        // Act
        services.AddConsulServiceDiscovery(config);
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ConsulOptions>>().Value;

        // Assert
        options.Url.Should().Be("http://localhost:8500");
        options.Token.Should().Be("test-token");
    }

    [Fact]
    public void AddConsulServiceDiscovery_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var config = CreateConfig();

        var act = () => services.AddConsulServiceDiscovery(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddConsulServiceDiscovery_NullConfig_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddConsulServiceDiscovery(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddConsulDestinationResolver_RegistersConsulResolverForInterface()
    {
        // Arrange — services.Replace 在无既有注册时退化为 Add，
        // 因此无需先注册 YARP 默认的 IDestinationResolver（其实现为 internal 无法直接引用）。
        // ConsulDestinationResolver 构造函数依赖 IConsulServiceDiscovery，需注册（mock 即可）。
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConsulServiceDiscovery>(_ => new Mock<IConsulServiceDiscovery>().Object);

        // Act
        services.AddConsulDestinationResolver();
        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<Yarp.ReverseProxy.ServiceDiscovery.IDestinationResolver>();

        // Assert
        resolver.Should().BeOfType<ConsulDestinationResolver>();
    }
}
