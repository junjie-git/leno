using Leno.Identity.Infrastructure.Dependencies;
using Leno.Infrastructure.Abstractions.Sessions;
using Leno.Infrastructure.Abstractions.UserAgent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Leno.Identity.Infrastructure.Tests.Dependencies;

public sealed class IdentityServiceCollectionExtensionsTests
{
    private static IServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = "Server=localhost,1433;Database=LenoIdentity;User Id=sa;Password=Test123!;TrustServerCertificate=True",
                ["OAuth2:AesKey"] = Convert.ToBase64String(new byte[32]),
                ["Identity:Jwt:Issuer"] = "leno-identity",
                ["Identity:Jwt:Audience"] = "leno-clients",
                ["Identity:Jwt:SigningKey"] = new string('a', 32),
                ["Identity:Jwt:AccessTokenExpirationMinutes"] = "30",
                ["Identity:Jwt:RefreshTokenExpirationDays"] = "7",
                ["ServiceUrls:AccessControlApi"] = "http://localhost:8082"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // RedisUserSessionStore 依赖 IConnectionMultiplexer，注册 mock 实例避免 DI 解析失败
        var multiplexer = new Mock<IConnectionMultiplexer>();
        services.AddSingleton<IConnectionMultiplexer>(multiplexer.Object);

        return services;
    }

    [Fact]
    public void AddIdentityInfrastructure_RegistersUserSessionStore()
    {
        var services = BuildServices();
        services.AddLogging();
        services.AddIdentityInfrastructure(services.BuildServiceProvider().GetRequiredService<IConfiguration>());
        var provider = services.BuildServiceProvider();

        var store = provider.GetService<IUserSessionStore>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddIdentityInfrastructure_RegistersUserAgentParser()
    {
        var services = BuildServices();
        services.AddLogging();
        services.AddIdentityInfrastructure(services.BuildServiceProvider().GetRequiredService<IConfiguration>());
        var provider = services.BuildServiceProvider();

        var parser = provider.GetService<IUserAgentParser>();
        parser.Should().NotBeNull();
    }
}
