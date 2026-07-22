using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.Cart.Infrastructure.Tests;

/// <summary>
/// CartSkuIndexService P1-5+P1-6 单元测试：
/// - P1-5：AddAsync 后调用 KeyExpireAsync 刷新 TTL 为 30 天，避免 stale 索引永久驻留
/// - P1-6：Redis 故障包装为 CartInfrastructureException 上抛，与 RedisAnonymousCartRepository 策略一致
/// </summary>
public class CartSkuIndexServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();

    public CartSkuIndexServiceTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
    }

    [Fact]
    public async Task AddAsync_ShouldCallKeyExpireWith30DaysTtlAfterSetAdd()
    {
        var sut = new CartSkuIndexService(_redisMock.Object, NullLogger<CartSkuIndexService>.Instance);
        var skuId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        await sut.AddAsync(skuId, cartId);

        _dbMock.Verify(
            d => d.SetAddAsync(It.Is<RedisKey>(k => (string)k == $"cart:sku:{skuId}"),
                It.Is<RedisValue>(v => (string)v == cartId.ToString()),
                It.IsAny<CommandFlags>()),
            Times.Once);
        _dbMock.Verify(
            d => d.KeyExpireAsync(It.Is<RedisKey>(k => (string)k == $"cart:sku:{skuId}"),
                It.Is<TimeSpan?>(t => t.HasValue && Math.Abs((t.Value - TimeSpan.FromDays(30)).TotalDays) < 1),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task AddAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new CartSkuIndexService(_redisMock.Object, NullLogger<CartSkuIndexService>.Instance);

        var act = () => sut.AddAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithInnerException<CartInfrastructureException, RedisConnectionException>();
    }

    [Fact]
    public async Task AddAsync_KeyExpireFailure_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _dbMock
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new CartSkuIndexService(_redisMock.Object, NullLogger<CartSkuIndexService>.Instance);

        var act = () => sut.AddAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }

    [Fact]
    public async Task RemoveAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new CartSkuIndexService(_redisMock.Object, NullLogger<CartSkuIndexService>.Instance);

        var act = () => sut.RemoveAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithInnerException<CartInfrastructureException, RedisConnectionException>();
    }

    [Fact]
    public async Task GetCartIdsBySkuAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new CartSkuIndexService(_redisMock.Object, NullLogger<CartSkuIndexService>.Instance);

        var act = () => sut.GetCartIdsBySkuAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithInnerException<CartInfrastructureException, RedisConnectionException>();
    }

    [Fact]
    public async Task GetCartIdsBySkuAsync_KeyNotExists_ShouldReturnEmptyList()
    {
        _dbMock
            .Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());
        var sut = new CartSkuIndexService(_redisMock.Object, NullLogger<CartSkuIndexService>.Instance);

        var result = await sut.GetCartIdsBySkuAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCartIdsBySkuAsync_ValidMembers_ShouldReturnParsedGuids()
    {
        var cartId1 = Guid.NewGuid();
        var cartId2 = Guid.NewGuid();
        _dbMock
            .Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { (string)cartId1.ToString(), (string)cartId2.ToString() });
        var sut = new CartSkuIndexService(_redisMock.Object, NullLogger<CartSkuIndexService>.Instance);

        var result = await sut.GetCartIdsBySkuAsync(Guid.NewGuid());

        result.Should().BeEquivalentTo(new[] { cartId1, cartId2 });
    }
}
