using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.Cart.Infrastructure.Tests;

/// <summary>
/// RedisAnonymousCartRepository 异常传播测试。
/// 验证 Redis 故障（RedisConnectionException / 一般异常）包装为 CartInfrastructureException 向上抛，
/// 不再静默吞掉掩盖故障导致调用方误判"购物车不存在"。
/// Key 不存在场景保持返回 null（合法的"购物车不存在"语义）。
/// </summary>
public class RedisAnonymousCartRepositoryTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();

    public RedisAnonymousCartRepositoryTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
    }

    [Fact]
    public async Task GetAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.GetAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithMessage("*匿名购物车暂不可用*")
            .WithInnerException<RedisConnectionException>();
    }

    [Fact]
    public async Task GetAsync_KeyNotExists_ShouldReturnNullWithoutThrowing()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.GetAsync("session-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);
        var cart = Cart.CreateAnonymous(Guid.NewGuid());

        var act = () => sut.SaveAsync("session-1", cart);

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithMessage("*匿名购物车暂不可用*");
    }

    [Fact]
    public async Task RemoveAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.RemoveAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }

    [Fact]
    public async Task RefreshTtlAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.RefreshTtlAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }

    [Fact]
    public async Task GetAsync_GeneralException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.GetAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }
}
