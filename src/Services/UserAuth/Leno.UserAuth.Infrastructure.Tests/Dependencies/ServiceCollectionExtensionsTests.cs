using Leno.Infrastructure.Auth;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Infrastructure.Dependencies;
using Leno.UserAuth.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Leno.UserAuth.Infrastructure.Tests.Dependencies;

public sealed class ServiceCollectionExtensionsTests
{
    private const string ConnectionString = "Server=localhost;Database=LenoUserAuth;Trusted_Connection=True;";

    [Fact]
    public void AddUserAuthInfrastructure_Should_Register_RedisRefreshTokenStore_By_Default()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAuthDb"] = ConnectionString,
                ["RefreshToken:Provider"] = "Redis"
            })
            .Build();

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);
        services.AddSingleton(multiplexerMock.Object);
        services.AddSingleton<JwtTokenGenerator>();
        services.AddLogging();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        services.AddSingleton(envMock.Object);

        // Act
        services.AddUserAuthInfrastructure(config);
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IRefreshTokenStore>();

        // Assert
        Assert.NotNull(store);
        Assert.IsType<RedisRefreshTokenStore>(store);
    }

    [Fact]
    public void AddUserAuthInfrastructure_Should_Register_InMemoryRefreshTokenStore_Only_When_Dev_And_InMemory_Configured()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAuthDb"] = ConnectionString,
                ["RefreshToken:Provider"] = "InMemory"
            })
            .Build();

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        services.AddSingleton(multiplexerMock.Object);
        services.AddSingleton<JwtTokenGenerator>();
        services.AddLogging();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        services.AddSingleton(envMock.Object);

        // Act
        services.AddUserAuthInfrastructure(config);
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IRefreshTokenStore>();

        // Assert
        Assert.NotNull(store);
        Assert.IsType<InMemoryRefreshTokenStore>(store);
    }

    [Fact]
    public void AddUserAuthInfrastructure_Should_Throw_When_InMemory_Configured_But_Production()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAuthDb"] = ConnectionString,
                ["RefreshToken:Provider"] = "InMemory"
            })
            .Build();

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        services.AddSingleton(multiplexerMock.Object);
        services.AddSingleton<JwtTokenGenerator>();
        services.AddLogging();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        services.AddSingleton(envMock.Object);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddUserAuthInfrastructure(config));
        Assert.Contains("InMemoryRefreshTokenStore", ex.Message);
        Assert.Contains("Development", ex.Message);
    }

    [Fact]
    public void AddUserAuthInfrastructure_Should_Throw_When_InMemory_Configured_But_No_HostEnvironment()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAuthDb"] = ConnectionString,
                ["RefreshToken:Provider"] = "InMemory"
            })
            .Build();

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        services.AddSingleton(multiplexerMock.Object);
        services.AddSingleton<JwtTokenGenerator>();
        services.AddLogging();

        // Act & Assert：未注册 IHostEnvironment，视为非 Development，应抛出
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddUserAuthInfrastructure(config));
        Assert.Contains("InMemoryRefreshTokenStore", ex.Message);
    }

    [Fact]
    public void AddUserAuthInfrastructure_Should_Throw_When_Provider_Unknown()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAuthDb"] = ConnectionString,
                ["RefreshToken:Provider"] = "Unknown"
            })
            .Build();

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        services.AddSingleton(multiplexerMock.Object);
        services.AddLogging();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        services.AddSingleton(envMock.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => services.AddUserAuthInfrastructure(config));
    }

    [Fact]
    public void AddUserAuthInfrastructure_Should_Default_To_Redis_When_Provider_Not_Set()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAuthDb"] = ConnectionString
            })
            .Build();

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);
        services.AddSingleton(multiplexerMock.Object);
        services.AddSingleton<JwtTokenGenerator>();
        services.AddLogging();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        services.AddSingleton(envMock.Object);

        // Act
        services.AddUserAuthInfrastructure(config);
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IRefreshTokenStore>();

        // Assert
        Assert.NotNull(store);
        Assert.IsType<RedisRefreshTokenStore>(store);
    }
}
