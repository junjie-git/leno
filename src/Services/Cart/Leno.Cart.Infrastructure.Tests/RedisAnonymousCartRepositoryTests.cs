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

    [Fact]
    public async Task SaveAsync_ShouldClearDomainEventsBeforeSerializationToAvoidRedisJsonMonotonicGrowth()
    {
        // P1-4：匿名购物车 _domainEvents 在 EF Core 落库路径由 SaveChangesWithOutboxAsync 清理，
        // 走 Redis 持久化路径时若不清理，每次 SaveAsync 都把累积事件序列化进 JSON，单调增长。
        var captured = string.Empty;
        _dbMock
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((_, v, _, _, _, _) => captured = (string)v!)
            .ReturnsAsync(true);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);
        var cart = Cart.CreateAnonymous(Guid.NewGuid());
        // AddItem 会发布 SkuAddedToCartEvent，构造有领域事件的状态
        cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
        cart.DomainEvents.Should().NotBeEmpty("预置：发布 SkuAddedToCartEvent 后应有领域事件");

        await sut.SaveAsync("session-1", cart);

        // 1) 调用方视角：SaveAsync 后聚合的领域事件已被清理
        cart.DomainEvents.Should().BeEmpty("SaveAsync 应清理领域事件");
        // 2) 序列化内容视角：JSON 中不应包含 domainEvents 字段
        captured.Should().NotContain("domainEvents", "序列化前应已清理领域事件，避免 Redis JSON 单调增长");
        captured.Should().NotContain("SkuAddedToCartEvent");
    }
}
