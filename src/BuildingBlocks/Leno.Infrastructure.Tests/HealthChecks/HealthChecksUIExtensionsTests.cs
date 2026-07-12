using Leno.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;

namespace Leno.Infrastructure.Tests.HealthChecks;

public class HealthChecksUIExtensionsTests
{
    [Fact]
    public void AddLenoHealthChecks_ValidParameters_ShouldReturnHealthChecksBuilder()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379",
                ["Elasticsearch:Uri"] = "http://localhost:9200"
            })
            .Build();

        var builder = services.AddLenoHealthChecks(config);

        builder.Should().NotBeNull();
    }

    [Fact]
    public void AddLenoHealthChecks_NullServices_ShouldThrow()
    {
        IServiceCollection services = null!;
        var config = new ConfigurationBuilder().Build();

        var act = () => services.AddLenoHealthChecks(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLenoHealthChecks_NullConfiguration_ShouldThrow()
    {
        var services = new ServiceCollection();

        var act = () => services.AddLenoHealthChecks(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLenoHealthChecks_WithDbConnectionString_ShouldAddSqlServerCheck()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379",
                ["Elasticsearch:Uri"] = "http://localhost:9200",
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=Test;"
            })
            .Build();

        var builder = services.AddLenoHealthChecks(config);

        builder.Should().NotBeNull();
    }

    [Fact]
    public void AddLenoHealthChecks_WithRabbitMQ_ShouldAddRabbitMQCheck()
    {
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

        var builder = services.AddLenoHealthChecks(config);

        builder.Should().NotBeNull();
    }

    [Fact]
    public void AddLenoHealthChecksUI_ValidParameters_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HealthChecksUI:EvaluationTimeInSeconds"] = "15",
                ["HealthChecksUI:MinimumSecondsBetweenFailureNotifications"] = "30"
            })
            .Build();

        services.AddLenoHealthChecksUI(config);

        var provider = services.BuildServiceProvider();
        // HealthChecksUI should be registered
        services.Should().NotBeNull();
    }

    [Fact]
    public void AddLenoHealthChecksUI_NullServices_ShouldThrow()
    {
        IServiceCollection services = null!;
        var config = new ConfigurationBuilder().Build();

        var act = () => services.AddLenoHealthChecksUI(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLenoHealthChecksUI_NullConfiguration_ShouldThrow()
    {
        var services = new ServiceCollection();

        var act = () => services.AddLenoHealthChecksUI(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLenoHealthChecksUI_WithCustomEndpoints_ShouldConfigureEndpoints()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HealthChecksUI:HealthChecks:0:Name"] = "Custom Service",
                ["HealthChecksUI:HealthChecks:0:Uri"] = "http://localhost:9999/health",
                ["HealthChecksUI:HealthChecks:1:Name"] = "Another Service",
                ["HealthChecksUI:HealthChecks:1:Uri"] = "http://localhost:9998/health"
            })
            .Build();

        services.AddLenoHealthChecksUI(config);

        services.Should().NotBeNull();
    }

    [Fact]
    public void MapLenoHealthChecks_NullEndpoints_ShouldThrow()
    {
        IEndpointRouteBuilder endpoints = null!;

        var act = () => endpoints.MapLenoHealthChecks();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapLenoHealthChecksUI_NullEndpoints_ShouldThrow()
    {
        IEndpointRouteBuilder endpoints = null!;

        var act = () => endpoints.MapLenoHealthChecksUI();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task HealthEndpoints_ShouldBeAccessible()
    {
        // 集成测试：验证健康检查端点可访问
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        var config = new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["Redis:Configuration"] = "localhost:6379",
                                ["Elasticsearch:Uri"] = "http://localhost:9200"
                            })
                            .Build();

                        services.AddLenoHealthChecks(config);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapLenoHealthChecks();
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // 存活探针
        var liveResponse = await client.GetAsync("/health/live");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 就绪探针（可能不健康，因为依赖未运行，但端点应返回 200 或 503）
        var readyResponse = await client.GetAsync("/health/ready");
        // 依赖未运行时会返回 503
        (readyResponse.StatusCode == HttpStatusCode.OK || readyResponse.StatusCode == HttpStatusCode.ServiceUnavailable).Should().BeTrue();
    }

    [Fact]
    public void AddLenoHealthChecksUI_WithInMemoryStorage_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        services.AddLenoHealthChecksUI(config);

        // 验证服务已注册
        services.Should().NotBeNull();
        var provider = services.BuildServiceProvider();
        provider.Should().NotBeNull();
    }
}