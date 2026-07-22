using Leno.ApiGateway.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Services;

/// <summary>
/// T28 单元测试：验证 RedisSlidingWindowRateLimiter 在 Redis 异常时记录 warning 日志（fail-open 仍放行）。
/// 不修改既有 RedisSlidingWindowRateLimiterTests 中的断言，仅新增针对日志输出的验证。
/// </summary>
public class RedisSlidingWindowRateLimiterLoggingTests
{
    /// <summary>
    /// T28：异步路径 Redis 异常时，应记录 warning 日志并 fail-open 放行。
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenRedisThrows_ShouldLogWarningAndFailOpen()
    {
        // Arrange
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        var loggerMock = new Mock<ILogger<RedisSlidingWindowRateLimiter>>();
        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object,
            key: "leno:rl:seckill:user-1",
            permitLimit: 50,
            window: TimeSpan.FromSeconds(1),
            segmentsPerWindow: 4,
            logger: loggerMock.Object);

        // Act
        var lease = await limiter.AcquireAsync(1);

        // Assert — fail-open：Redis 不可用时放行
        lease.IsAcquired.Should().BeTrue();

        // 应记录 warning 日志，包含异常对象
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// T28：异步路径 warning 日志应包含 Redis key 信息，便于运维定位故障分区。
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenRedisThrows_LogMessageShouldContainKey()
    {
        // Arrange
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisTimeoutException("timeout", CommandStatus.Unknown));

        var loggerMock = new Mock<ILogger<RedisSlidingWindowRateLimiter>>();
        string? loggedMessage = null;
        loggerMock
            .Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((level, _, state, _, _) =>
            {
                if (level == LogLevel.Warning)
                {
                    loggedMessage = state.ToString();
                }
            });

        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object,
            key: "leno:rl:seckill:user-42",
            permitLimit: 50,
            window: TimeSpan.FromSeconds(1),
            segmentsPerWindow: 4,
            logger: loggerMock.Object);

        // Act
        await limiter.AcquireAsync(1);

        // Assert：日志应包含 Redis key
        loggedMessage.Should().NotBeNull();
        loggedMessage!.Should().Contain("leno:rl:seckill:user-42");
    }

    /// <summary>
    /// T28：同步路径（AttemptAcquireCore → TryAcquireSync）Redis 异常时也应记录 warning 日志。
    /// </summary>
    [Fact]
    public void AttemptAcquire_WhenRedisThrows_ShouldLogWarningAndFailOpen()
    {
        // Arrange
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluate(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        var loggerMock = new Mock<ILogger<RedisSlidingWindowRateLimiter>>();
        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object,
            key: "leno:rl:default:client-x",
            permitLimit: 200,
            window: TimeSpan.FromSeconds(1),
            segmentsPerWindow: 4,
            logger: loggerMock.Object);

        // Act — 同步路径
        var lease = limiter.AttemptAcquire(1);

        // Assert
        lease.IsAcquired.Should().BeTrue();
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// T28：Redis 正常返回时不应记录 warning 日志。
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenRedisSucceeds_ShouldNotLogWarning()
    {
        // Arrange
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1L, ResultType.Integer));

        var loggerMock = new Mock<ILogger<RedisSlidingWindowRateLimiter>>();
        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object,
            key: "leno:rl:seckill:user-1",
            permitLimit: 50,
            window: TimeSpan.FromSeconds(1),
            segmentsPerWindow: 4,
            logger: loggerMock.Object);

        // Act
        var lease = await limiter.AcquireAsync(1);

        // Assert：放行，且无 warning 日志
        lease.IsAcquired.Should().BeTrue();
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// T28：logger 参数为 null 时（向后兼容路径）应使用 NullLogger，不抛异常。
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenLoggerNull_ShouldUseNullLoggerAndNotThrow()
    {
        // Arrange
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        // logger 参数为 null（等价于不传）
        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object,
            key: "leno:rl:seckill:user-1",
            permitLimit: 50,
            window: TimeSpan.FromSeconds(1),
            segmentsPerWindow: 4,
            logger: null);

        // Act
        var lease = await limiter.AcquireAsync(1);

        // Assert：不抛异常，fail-open 放行
        lease.IsAcquired.Should().BeTrue();
    }

    /// <summary>
    /// T28：异步路径 OperationCanceledException 不应被 fail-open catch 吞掉，应向上抛出。
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenCancelled_ShouldPropagateCancellationAndNotLogWarning()
    {
        // Arrange
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new OperationCanceledException());

        var loggerMock = new Mock<ILogger<RedisSlidingWindowRateLimiter>>();
        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object,
            key: "leno:rl:seckill:user-1",
            permitLimit: 50,
            window: TimeSpan.FromSeconds(1),
            segmentsPerWindow: 4,
            logger: loggerMock.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await limiter.AcquireAsync(1, cts.Token);

        // Assert：OperationCanceledException 向上抛出，不记录 warning
        await act.Should().ThrowAsync<OperationCanceledException>();
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// T28：warning 日志应包含异常对象本身（便于 Serilog 等记录 StackTrace）。
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenRedisThrows_LogShouldContainException()
    {
        // Arrange
        var expectedException = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "boom");
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(expectedException);

        var loggerMock = new Mock<ILogger<RedisSlidingWindowRateLimiter>>();
        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object,
            key: "key1",
            permitLimit: 50,
            window: TimeSpan.FromSeconds(1),
            segmentsPerWindow: 4,
            logger: loggerMock.Object);

        // Act
        await limiter.AcquireAsync(1);

        // Assert：Log 调用的第 4 个参数（exception）应为原异常对象
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.Is<Exception>(ex => ReferenceEquals(ex, expectedException)),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
