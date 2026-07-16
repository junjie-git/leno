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

    // ===== T23: InvalidatePatternAsync — UNLINK + 分批 SCAN 测试 =====

    /// <summary>
    /// T23：InvalidatePatternAsync 应使用 UNLINK 而非 DEL 删除匹配 key。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_ShouldUseUnlinkNotDel()
    {
        // Arrange
        var (redisMock, databaseMock, subscriber) = CreateSubscriberWithServer(
            new[] { (RedisKey)"leno:cache:user:1", (RedisKey)"leno:cache:user:2" });

        // Act
        await subscriber.InvalidatePatternAsync(databaseMock.Object, "user:*");

        // Assert：使用 UNLINK（ExecuteAsync）而非 DEL（KeyDeleteAsync）
        databaseMock.Verify(
            d => d.ExecuteAsync("UNLINK", It.IsAny<ICollection<object>>(), It.IsAny<CommandFlags>()),
            Times.Once);
        databaseMock.Verify(
            d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);

        subscriber.Dispose();
    }

    /// <summary>
    /// T23：多个 key（少于批次大小）应合并为一次 UNLINK 调用。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_MultipleKeysBelowBatchSize_ShouldUnlinkInSingleCall()
    {
        // Arrange：5 个 key，默认批次 100，应合并为一次 UNLINK
        var keys = Enumerable.Range(1, 5).Select(i => (RedisKey)$"leno:cache:user:{i}").ToArray();
        var (redisMock, databaseMock, subscriber) = CreateSubscriberWithServer(keys);

        // Act
        await subscriber.InvalidatePatternAsync(databaseMock.Object, "user:*");

        // Assert：仅一次 UNLINK 调用
        databaseMock.Verify(
            d => d.ExecuteAsync("UNLINK", It.IsAny<ICollection<object>>(), It.IsAny<CommandFlags>()),
            Times.Once);

        subscriber.Dispose();
    }

    /// <summary>
    /// T23：多个 key（超过批次大小）应分批 UNLINK。
    /// 默认批次 100，250 个 key 应分为 3 批（100 + 100 + 50）。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_MultipleKeysExceedBatchSize_ShouldUnlinkInMultipleBatches()
    {
        // Arrange：250 个 key，默认批次 100 → 3 次 UNLINK
        var keys = Enumerable.Range(1, 250).Select(i => (RedisKey)$"leno:cache:user:{i}").ToArray();
        var (redisMock, databaseMock, subscriber) = CreateSubscriberWithServer(keys);

        // Act
        await subscriber.InvalidatePatternAsync(databaseMock.Object, "user:*");

        // Assert：3 次 UNLINK 调用（100 + 100 + 50）
        databaseMock.Verify(
            d => d.ExecuteAsync("UNLINK", It.IsAny<ICollection<object>>(), It.IsAny<CommandFlags>()),
            Times.Exactly(3));

        subscriber.Dispose();
    }

    /// <summary>
    /// T23：自定义批次大小覆盖应生效。
    /// 10 个 key，批次大小 3 → 4 次 UNLINK（3+3+3+1）。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_CustomBatchSize_ShouldBatchByCustomSize()
    {
        // Arrange：10 个 key，自定义批次 3 → 4 次 UNLINK
        var keys = Enumerable.Range(1, 10).Select(i => (RedisKey)$"leno:cache:user:{i}").ToArray();
        var (redisMock, databaseMock, subscriber) = CreateSubscriberWithServer(keys);
        subscriber.PatternInvalidationBatchSizeOverride = 3;

        // Act
        await subscriber.InvalidatePatternAsync(databaseMock.Object, "user:*");

        // Assert：4 次 UNLINK 调用（3+3+3+1）
        databaseMock.Verify(
            d => d.ExecuteAsync("UNLINK", It.IsAny<ICollection<object>>(), It.IsAny<CommandFlags>()),
            Times.Exactly(4));

        subscriber.Dispose();
    }

    /// <summary>
    /// T23：无匹配 key 时不应调用 UNLINK。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_NoMatchingKeys_ShouldNotCallUnlink()
    {
        // Arrange
        var (redisMock, databaseMock, subscriber) = CreateSubscriberWithServer(Array.Empty<RedisKey>());

        // Act
        await subscriber.InvalidatePatternAsync(databaseMock.Object, "nonexistent:*");

        // Assert
        databaseMock.Verify(
            d => d.ExecuteAsync("UNLINK", It.IsAny<ICollection<object>>(), It.IsAny<CommandFlags>()),
            Times.Never);

        subscriber.Dispose();
    }

    /// <summary>
    /// T23：UNLINK 命令参数应包含匹配的 key（字符串形式）。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_ShouldPassKeysAsUnlinkArgs()
    {
        // Arrange
        var keys = new[]
        {
            (RedisKey)"leno:cache:user:1",
            (RedisKey)"leno:cache:user:2",
            (RedisKey)"leno:cache:user:3"
        };
        var (redisMock, databaseMock, subscriber) = CreateSubscriberWithServer(keys);
        List<object>? capturedArgs = null;
        databaseMock
            .Setup(d => d.ExecuteAsync("UNLINK", It.IsAny<ICollection<object>>(), It.IsAny<CommandFlags>()))
            .Callback<string, ICollection<object>, CommandFlags>((_, args, _) => capturedArgs = args.ToList())
            .ReturnsAsync(RedisResult.Create(3));

        // Act
        await subscriber.InvalidatePatternAsync(databaseMock.Object, "user:*");

        // Assert：UNLINK 参数包含全部 3 个 key
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Count.Should().Be(3);
        capturedArgs.Should().ContainEquivalentOf("leno:cache:user:1");
        capturedArgs.Should().ContainEquivalentOf("leno:cache:user:2");
        capturedArgs.Should().ContainEquivalentOf("leno:cache:user:3");

        subscriber.Dispose();
    }

    /// <summary>
    /// T23：Pattern 应拼接 KeyPrefix（leno:cache:）后传给 SCAN。
    /// 通过验证 SCAN 返回的 key 全部被 UNLINK（说明 SCAN 用了正确 pattern）。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_ShouldPrependKeyPrefixToPattern()
    {
        // Arrange：SCAN 返回带前缀的 key，证明 SCAN 使用了 leno:cache: + pattern
        var keys = new[] { (RedisKey)"leno:cache:product:*" };
        var (redisMock, databaseMock, subscriber) = CreateSubscriberWithServer(keys, expectedPattern: "leno:cache:product:*");

        // Act
        await subscriber.InvalidatePatternAsync(databaseMock.Object, "product:*");

        // Assert：UNLINK 调用一次，包含带前缀的 key
        databaseMock.Verify(
            d => d.ExecuteAsync("UNLINK", It.IsAny<ICollection<object>>(), It.IsAny<CommandFlags>()),
            Times.Once);

        subscriber.Dispose();
    }

    /// <summary>
    /// 构造 CacheInvalidationSubscriber + IServer mock，使 KeysAsync 返回指定 key 序列。
    /// </summary>
    private static (Mock<IConnectionMultiplexer> redisMock, Mock<IDatabase> databaseMock, CacheInvalidationSubscriber subscriber) CreateSubscriberWithServer(
        RedisKey[] keys,
        string? expectedPattern = null)
    {
        var redisMock = new Mock<IConnectionMultiplexer>();
        var databaseMock = new Mock<IDatabase>();
        var serverMock = new Mock<IServer>();

        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(databaseMock.Object);
        redisMock.Setup(r => r.GetServers()).Returns(new[] { serverMock.Object });
        serverMock.SetupGet(s => s.IsReplica).Returns(false);

        // 如果指定了期望的 pattern，则验证 SCAN 使用了正确的 pattern
        if (expectedPattern is not null)
        {
            serverMock
                .Setup(s => s.KeysAsync(
                    It.IsAny<int>(),
                    It.Is<RedisValue>(p => p == expectedPattern),
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<int>(),
                    It.IsAny<CommandFlags>()))
                .Returns(CreateKeyAsyncEnumerable(keys));
        }
        else
        {
            serverMock
                .Setup(s => s.KeysAsync(
                    It.IsAny<int>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<int>(),
                    It.IsAny<CommandFlags>()))
                .Returns(CreateKeyAsyncEnumerable(keys));
        }

        databaseMock
            .Setup(d => d.ExecuteAsync("UNLINK", It.IsAny<ICollection<object>>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(keys.Length));

        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, NullLogger<CacheInvalidationSubscriber>.Instance);

        return (redisMock, databaseMock, subscriber);
    }

    /// <summary>
    /// 创建 yield 模式的 IAsyncEnumerable&lt;RedisKey&gt;，模拟 SCAN 迭代。
    /// </summary>
    private static async IAsyncEnumerable<RedisKey> CreateKeyAsyncEnumerable(IEnumerable<RedisKey> keys)
    {
        foreach (var key in keys)
        {
            await Task.Yield();
            yield return key;
        }
    }
}
