using Leno.Infrastructure.Dependencies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Tests.HealthChecks;

/// <summary>
/// T20 验证：AddLenoInfrastructure 基础路径也注册 RabbitMQ 健康检查。
/// </summary>
public class AddHealthChecksRabbitMqRegistrationTests
{
    [Fact]
    public void AddLenoInfrastructure_WithRabbitMqConfig_RegistersRabbitMqHealthCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379",
                ["Elasticsearch:Uri"] = "http://localhost:9200",
                ["RabbitMQ:Host"] = "localhost",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest"
            })
            .Build();

        // Act — 仅调用 AddLenoInfrastructure（不走 AddLenoFullHealthChecks）
        services.AddLenoInfrastructure(config);

        // Assert — RabbitMQ 健康检查应已注册
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var registrations = options.Registrations;
        registrations.Should().Contain(r => r.Name == "rabbitmq",
            "AddLenoInfrastructure 基础路径应在配置 RabbitMQ:Host 时注册 rabbitmq 健康检查");
    }

    [Fact]
    public void AddLenoInfrastructure_WithoutRabbitMqConfig_DoesNotRegisterRabbitMqHealthCheck()
    {
        // Arrange — 不配置 RabbitMQ:Host
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379",
                ["Elasticsearch:Uri"] = "http://localhost:9200"
            })
            .Build();

        // Act
        services.AddLenoInfrastructure(config);

        // Assert — 不应注册 rabbitmq 健康检查
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        options.Registrations.Should().NotContain(r => r.Name == "rabbitmq");
    }

    [Fact]
    public void AddLenoInfrastructure_AlwaysRegistersSelfRedisElasticsearch()
    {
        // Arrange — 验证基础健康检查始终注册
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379",
                ["Elasticsearch:Uri"] = "http://localhost:9200"
            })
            .Build();

        // Act
        services.AddLenoInfrastructure(config);

        // Assert
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var names = options.Registrations.Select(r => r.Name).ToList();
        names.Should().Contain("self");
        names.Should().Contain("redis");
        names.Should().Contain("elasticsearch");
    }

    [Fact]
    public void AddLenoInfrastructure_RabbitMqCheckHasReadyTag()
    {
        // Arrange — 验证 rabbitmq 健康检查带 "ready" tag，纳入就绪探针
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379",
                ["Elasticsearch:Uri"] = "http://localhost:9200",
                ["RabbitMQ:Host"] = "rabbitmq-host",
                ["RabbitMQ:Port"] = "5672"
            })
            .Build();

        // Act
        services.AddLenoInfrastructure(config);

        // Assert
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var rabbitMqRegistration = options.Registrations.FirstOrDefault(r => r.Name == "rabbitmq");
        rabbitMqRegistration.Should().NotBeNull();
        rabbitMqRegistration!.Tags.Should().Contain("ready");
    }
}
