using System.Text.Json;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Services;

public class CacheInvalidationSubscriberTests
{
    [Fact]
    public async Task StartAsync_SubscribesToInvalidationChannel()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var subscriberMock = new Mock<ISubscriber>();

        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>()))
            .Returns(subscriberMock.Object);

        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, NullLogger<CacheInvalidationSubscriber>.Instance);

        // Act
        await subscriber.StartAsync(CancellationToken.None);

        // Assert
        subscriberMock.Verify(
            s => s.Subscribe(
                It.Is<RedisChannel>(c => c == RedisChannel.Literal(CacheInvalidationSubscriber.ChannelName)),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromChannel()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var subscriberMock = new Mock<ISubscriber>();

        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>()))
            .Returns(subscriberMock.Object);

        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, NullLogger<CacheInvalidationSubscriber>.Instance);

        await subscriber.StartAsync(CancellationToken.None);

        // Act
        await subscriber.StopAsync(CancellationToken.None);

        // Assert
        subscriberMock.Verify(
            s => s.UnsubscribeAll(It.IsAny<CommandFlags>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ParseInvalidationEvent_WithCacheKey_ReturnsCorrectKey()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new
        {
            eventType = "CacheInvalidated",
            cacheKey = "GET:/api/products/123::42",
            pattern = (string?)null
        });

        // Act
        var evt = JsonSerializer.Deserialize<CacheInvalidatedEvent>(json);

        // Assert
        evt!.EventType.Should().Be("CacheInvalidated");
        evt.CacheKey.Should().Be("GET:/api/products/123::42");
        evt.Pattern.Should().BeNull();
    }

    [Fact]
    public void ParseInvalidationEvent_WithPattern_ReturnsCorrectPattern()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new
        {
            eventType = "CacheInvalidated",
            cacheKey = (string?)null,
            pattern = "/api/product/sku/123*"
        });

        // Act
        var evt = JsonSerializer.Deserialize<CacheInvalidatedEvent>(json);

        // Assert
        evt!.Pattern.Should().Be("/api/product/sku/123*");
        evt.CacheKey.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_WhenRedisThrows_LogsButDoesNotThrow()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>()))
            .Throws(new InvalidOperationException("Redis unavailable"));

        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, NullLogger<CacheInvalidationSubscriber>.Instance);

        // Act
        var act = async () => await subscriber.StartAsync(CancellationToken.None);

        // Assert — 不抛出异常，由 HostedService 健康检查兜底
        await act.Should().NotThrowAsync();
    }
}
