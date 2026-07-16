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

    // ===== T21.1: ConnectionFailed / InternalError 自动重连测试 =====

    /// <summary>
    /// T21.1：StartAsync 应订阅 IConnectionMultiplexer 的 ConnectionFailed 事件。
    /// </summary>
    [Fact]
    public async Task StartAsync_ShouldAttachConnectionFailedEventHandler()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var subscriberMock = new Mock<ISubscriber>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subscriberMock.Object);

        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, NullLogger<CacheInvalidationSubscriber>.Instance);

        // Act
        await subscriber.StartAsync(CancellationToken.None);

        // Assert
        redisMock.VerifyAdd(
            r => r.ConnectionFailed += It.IsAny<EventHandler<ConnectionFailedEventArgs>>(),
            Times.Once);

        subscriber.Dispose();
    }

    /// <summary>
    /// T21.1：StartAsync 应订阅 IConnectionMultiplexer 的 InternalError 事件。
    /// </summary>
    [Fact]
    public async Task StartAsync_ShouldAttachInternalErrorEventHandler()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var subscriberMock = new Mock<ISubscriber>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subscriberMock.Object);

        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, NullLogger<CacheInvalidationSubscriber>.Instance);

        // Act
        await subscriber.StartAsync(CancellationToken.None);

        // Assert
        redisMock.VerifyAdd(
            r => r.InternalError += It.IsAny<EventHandler<InternalErrorEventArgs>>(),
            Times.Once);

        subscriber.Dispose();
    }

    /// <summary>
    /// T21.1：StopAsync 应解绑 ConnectionFailed / InternalError 事件处理器。
    /// </summary>
    [Fact]
    public async Task StopAsync_ShouldDetachConnectionEventHandlers()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var subscriberMock = new Mock<ISubscriber>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subscriberMock.Object);

        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, NullLogger<CacheInvalidationSubscriber>.Instance);
        await subscriber.StartAsync(CancellationToken.None);

        // Act
        await subscriber.StopAsync(CancellationToken.None);

        // Assert
        redisMock.VerifyRemove(
            r => r.ConnectionFailed -= It.IsAny<EventHandler<ConnectionFailedEventArgs>>(),
            Times.Once);
        redisMock.VerifyRemove(
            r => r.InternalError -= It.IsAny<EventHandler<InternalErrorEventArgs>>(),
            Times.Once);
    }

    /// <summary>
    /// T21.1：ConnectionFailed 事件触发后应尝试重新订阅通道（指数退避后重订阅）。
    /// 使用短重连延迟覆盖避免测试等待 1s。
    /// </summary>
    [Fact]
    public async Task ConnectionFailed_ShouldTriggerResubscribeWithBackoff()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var subscriberMock = new Mock<ISubscriber>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subscriberMock.Object);

        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, NullLogger<CacheInvalidationSubscriber>.Instance);
        // 短重连延迟以加速测试
        subscriber.ReconnectInitialDelayOverride = TimeSpan.FromMilliseconds(10);

        await subscriber.StartAsync(CancellationToken.None);
        // StartAsync 后 Subscribe 调用 1 次
        subscriberMock.Verify(
            s => s.Subscribe(
                It.Is<RedisChannel>(c => c == RedisChannel.Literal(CacheInvalidationSubscriber.ChannelName)),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()),
            Times.Once);

        // Act：模拟连接失败事件
        var connectionFailedArgs = CreateConnectionFailedEventArgs();
        redisMock.Raise(
            r => r.ConnectionFailed += null,
            redisMock.Object,
            connectionFailedArgs);

        // 等待指数退避延迟 + 重连完成
        await Task.Delay(200, CancellationToken.None);

        // Assert：Subscribe 被再次调用（重连后重新订阅）
        subscriberMock.Verify(
            s => s.Subscribe(
                It.Is<RedisChannel>(c => c == RedisChannel.Literal(CacheInvalidationSubscriber.ChannelName)),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()),
            Times.AtLeast(2));

        subscriber.Dispose();
    }

    /// <summary>
    /// T21.1：InternalError 事件触发后同样应尝试重新订阅通道。
    /// </summary>
    [Fact]
    public async Task InternalError_ShouldTriggerResubscribeWithBackoff()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var subscriberMock = new Mock<ISubscriber>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subscriberMock.Object);

        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, NullLogger<CacheInvalidationSubscriber>.Instance);
        subscriber.ReconnectInitialDelayOverride = TimeSpan.FromMilliseconds(10);

        await subscriber.StartAsync(CancellationToken.None);

        // Act：模拟内部错误事件
        var internalErrorArgs = CreateInternalErrorEventArgs();
        redisMock.Raise(
            r => r.InternalError += null,
            redisMock.Object,
            internalErrorArgs);

        // 等待指数退避延迟 + 重连完成
        await Task.Delay(200, CancellationToken.None);

        // Assert：Subscribe 被再次调用
        subscriberMock.Verify(
            s => s.Subscribe(
                It.Is<RedisChannel>(c => c == RedisChannel.Literal(CacheInvalidationSubscriber.ChannelName)),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()),
            Times.AtLeast(2));

        subscriber.Dispose();
    }

    /// <summary>
    /// 使用反射构造 ConnectionFailedEventArgs（StackExchange.Redis 2.8.x 构造函数签名兼容）。
    /// </summary>
    private static ConnectionFailedEventArgs CreateConnectionFailedEventArgs()
    {
        // ConnectionFailedEventArgs 继承自 InternalErrorEventArgs，构造函数签名在不同版本略有差异
        // 优先尝试 (EndPoint, ConnectionType, FailureType, Exception, string?) 签名
        var type = typeof(ConnectionFailedEventArgs);
        var constructors = type.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();
            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = parameters[i].ParameterType switch
                {
                    var t when t == typeof(Exception) => new InvalidOperationException("模拟连接断开"),
                    var t when t == typeof(string) => null,
                    _ => null
                };
            }

            try
            {
                return (ConnectionFailedEventArgs)ctor.Invoke(args);
            }
            catch
            {
                // 尝试下一个构造函数
            }
        }

        throw new InvalidOperationException("无法构造 ConnectionFailedEventArgs，请检查 StackExchange.Redis 版本");
    }

    /// <summary>
    /// 使用反射构造 InternalErrorEventArgs。
    /// </summary>
    private static InternalErrorEventArgs CreateInternalErrorEventArgs()
    {
        var type = typeof(InternalErrorEventArgs);
        var constructors = type.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();
            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = parameters[i].ParameterType switch
                {
                    var t when t == typeof(Exception) => new InvalidOperationException("模拟内部错误"),
                    var t when t == typeof(string) => null,
                    _ => null
                };
            }

            try
            {
                return (InternalErrorEventArgs)ctor.Invoke(args);
            }
            catch
            {
                // 尝试下一个构造函数
            }
        }

        throw new InvalidOperationException("无法构造 InternalErrorEventArgs，请检查 StackExchange.Redis 版本");
    }
}
