using Leno.ApiGateway.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace Leno.ApiGateway.Tests.Services;

public class JwtBlacklistServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ISubscriber> _subscriberMock;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<JwtBlacklistService> _logger;

    public JwtBlacklistServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _subscriberMock = new Mock<ISubscriber>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = NullLogger<JwtBlacklistService>.Instance;

        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
        _redisMock.Setup(x => x.GetSubscriber(It.IsAny<object>())).Returns(_subscriberMock.Object);
    }

    [Fact]
    public async Task RevokeAsync_ShouldPublishInvalidationNotification()
    {
        // Arrange
        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);
        var jti = "test-jti-123";
        var ttl = TimeSpan.FromMinutes(30);

        _dbMock.Setup(x => x.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString() == $"leno:jwt:blacklist:{jti}"),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        RedisChannel publishedChannel = default;
        RedisValue publishedValue = default;
        _subscriberMock.Setup(x => x.PublishAsync(
            It.IsAny<RedisChannel>(),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, RedisValue, CommandFlags>((ch, val, _) =>
            {
                publishedChannel = ch;
                publishedValue = val;
            })
            .ReturnsAsync(1);

        // Act
        await service.RevokeAsync(jti, ttl, CancellationToken.None);

        // Assert — Pub/Sub 通知已发布
        publishedChannel.ToString().Should().Be(JwtBlacklistService.InvalidationChannel);
        publishedValue.ToString().Should().Contain(jti);
    }

    [Fact]
    public async Task IsRevokedAsync_LocalCacheHit_ShouldNotQueryRedis()
    {
        // Arrange — 本地缓存已有 jti
        var jti = "cached-jti";
        _memoryCache.Set($"jwt_bl:{jti}", true, TimeSpan.FromMinutes(5));
        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);

        // Act
        var result = await service.IsRevokedAsync(jti, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _dbMock.Verify(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never,
            "本地缓存命中时不应查询 Redis");
    }

    [Fact]
    public async Task IsRevokedAsync_LocalCacheMiss_RedisHit_ShouldPopulateLocalCacheWithTtl()
    {
        // Arrange
        var jti = "redis-only-jti";
        _dbMock.Setup(x => x.KeyExistsAsync(
            It.Is<RedisKey>(k => k.ToString() == $"leno:jwt:blacklist:{jti}"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _dbMock.Setup(x => x.KeyTimeToLiveAsync(
            It.Is<RedisKey>(k => k.ToString() == $"leno:jwt:blacklist:{jti}"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync((TimeSpan?)TimeSpan.FromMinutes(20));

        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);

        // Act
        var result = await service.IsRevokedAsync(jti, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _memoryCache.TryGetValue($"jwt_bl:{jti}", out bool cached).Should().BeTrue();
        cached.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ShouldSubscribeToInvalidationChannel()
    {
        // Arrange
        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — 订阅 Pub/Sub 通道
        _subscriberMock.Verify(x => x.SubscribeAsync(
            It.Is<RedisChannel>(ch => ch.ToString() == JwtBlacklistService.InvalidationChannel),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void OnInvalidationMessage_ShouldPopulateLocalCache()
    {
        // Arrange
        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);
        var jti = "remote-revoked-jti";
        var message = new RedisValue(System.Text.Json.JsonSerializer.Serialize(
            new JwtBlacklistInvalidationEvent { Jti = jti, TtlSeconds = 1800 }));

        // Act — 模拟收到 Pub/Sub 消息
        service.HandleInvalidationMessage(default, message);

        // Assert
        _memoryCache.TryGetValue($"jwt_bl:{jti}", out bool cached).Should().BeTrue();
        cached.Should().BeTrue();
    }
}

/// <summary>测试用的黑名单失效事件 DTO（PascalCase，与发布端一致）。</summary>
public sealed class JwtBlacklistInvalidationEvent
{
    public string Jti { get; set; } = string.Empty;
    public long TtlSeconds { get; set; }
}
