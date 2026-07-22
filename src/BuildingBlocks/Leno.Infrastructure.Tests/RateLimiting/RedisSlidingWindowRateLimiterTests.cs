using Leno.Infrastructure.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.Infrastructure.Tests.RateLimiting;

public class RedisSlidingWindowRateLimiterTests
{
    private static Mock<IConnectionMultiplexer> CreateRedisMock(long scriptResult)
    {
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)scriptResult, ResultType.Integer));

        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);
        return redisMock;
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRedisReturnsNonZero_GrantsAndReturnsCount()
    {
        // Arrange — Lua 脚本返回 3 表示当前窗口内 3 个请求
        var redisMock = CreateRedisMock(scriptResult: 3);
        var limiter = new RedisSlidingWindowRateLimiter(redisMock.Object);

        // Act
        var result = await limiter.TryAcquireAsync("seckill:user-1", 50, TimeSpan.FromSeconds(60));

        // Assert
        result.Allowed.Should().BeTrue("Lua 返回非零值表示允许");
        result.CurrentCount.Should().Be(3, "返回值为当前窗口计数");
        result.Limit.Should().Be(50);
        result.ResetAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRedisReturnsZero_Denies()
    {
        // Arrange — Lua 脚本返回 0 表示拒绝
        var redisMock = CreateRedisMock(scriptResult: 0);
        var limiter = new RedisSlidingWindowRateLimiter(redisMock.Object);

        // Act
        var result = await limiter.TryAcquireAsync("seckill:user-1", 50, TimeSpan.FromSeconds(60));

        // Assert
        result.Allowed.Should().BeFalse("Lua 返回 0 表示限流拒绝");
        result.ResetAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRedisThrows_FailsOpen()
    {
        // Arrange — Redis 异常时降级放行，避免 Redis 故障阻断全部流量
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);

        var limiter = new RedisSlidingWindowRateLimiter(redisMock.Object);

        // Act
        var result = await limiter.TryAcquireAsync("seckill:user-1", 50, TimeSpan.FromSeconds(60));

        // Assert — fail-open：Redis 不可用时放行
        result.Allowed.Should().BeTrue("Redis 故障时应 fail-open 放行");
    }

    [Fact]
    public async Task TryAcquireAsync_WhenCancelled_PropagatesCancellation()
    {
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new OperationCanceledException());

        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);

        var limiter = new RedisSlidingWindowRateLimiter(redisMock.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await limiter.TryAcquireAsync("seckill:user-1", 50, TimeSpan.FromSeconds(60), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullRedis_Throws()
    {
        var act = () => new RedisSlidingWindowRateLimiter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task TryAcquireAsync_NullOrEmptyKey_Throws(string? key)
    {
        var redisMock = CreateRedisMock(scriptResult: 1);
        var limiter = new RedisSlidingWindowRateLimiter(redisMock.Object);

        var act = async () => await limiter.TryAcquireAsync(key!, 50, TimeSpan.FromSeconds(60));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryAcquireAsync_NonPositivePermitLimit_Throws(int limit)
    {
        var redisMock = CreateRedisMock(scriptResult: 1);
        var limiter = new RedisSlidingWindowRateLimiter(redisMock.Object);

        var act = async () => await limiter.TryAcquireAsync("key", limit, TimeSpan.FromSeconds(60));
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task TryAcquireAsync_NonPositiveWindow_Throws()
    {
        var redisMock = CreateRedisMock(scriptResult: 1);
        var limiter = new RedisSlidingWindowRateLimiter(redisMock.Object);

        var act = async () => await limiter.TryAcquireAsync("key", 50, TimeSpan.Zero);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task TryAcquireAsync_PassesCorrectKeyWithPrefixToRedis()
    {
        // Arrange
        RedisKey[]? capturedKeys = null;
        RedisValue[]? capturedValues = null;

        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, keys, values, _) =>
            {
                capturedKeys = keys;
                capturedValues = values;
            })
            .ReturnsAsync(RedisResult.Create((RedisValue)1L, ResultType.Integer));

        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(dbMock.Object);

        var limiter = new RedisSlidingWindowRateLimiter(redisMock.Object);

        // Act
        await limiter.TryAcquireAsync("seckill:user-1", 50, TimeSpan.FromSeconds(60));

        // Assert — 键应包含 leno:ratelimit: 前缀
        capturedKeys.Should().NotBeNull();
        capturedKeys![0].ToString().Should().Be("leno:ratelimit:seckill:user-1");

        capturedValues.Should().NotBeNull();
        capturedValues!.Length.Should().Be(5, "应有 5 个 ARGV：nowMs, windowStartMs, member, permitLimit, ttlSeconds");
        // ARGV[4] = permit limit
        ((long)capturedValues[3]).Should().Be(50);
        // ARGV[5] = TTL seconds (60s * 1.1 = 66s)
        ((long)capturedValues[4]).Should().Be(66);
    }

    [Fact]
    public void Script_ClearsWindowBeforeCounting_FixesBoundaryMisjudgment()
    {
        // Arrange — 验证 Lua 脚本顺序：先 ZREMRANGEBYSCORE 清除窗口外记录，再 ZCARD 计数
        // 这是 fix-12 P0-T8 修复的关键：原实现先 ZCARD 后 ZREMRANGEBYSCORE 导致窗口边界误拒
        var script = RedisSlidingWindowRateLimiter.GetScriptForTesting();

        // Assert — ZREMRANGEBYSCORE 必须出现在 ZCARD 之前
        var removeIndex = script.IndexOf("ZREMRANGEBYSCORE", StringComparison.Ordinal);
        var cardIndex = script.IndexOf("ZCARD", StringComparison.Ordinal);

        removeIndex.Should().BeGreaterThanOrEqualTo(0, "脚本应包含 ZREMRANGEBYSCORE");
        cardIndex.Should().BeGreaterThanOrEqualTo(0, "脚本应包含 ZCARD");
        removeIndex.Should().BeLessThan(cardIndex,
            "ZREMRANGEBYSCORE 必须在 ZCARD 之前，先清除窗口外记录再计数，避免窗口边界误拒");
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullLogger_DoesNotThrow()
    {
        // Arrange — 不传 logger 时应使用 NullLogger，不应抛异常
        var redisMock = CreateRedisMock(scriptResult: 1);

        var limiter = new RedisSlidingWindowRateLimiter(redisMock.Object, logger: null);

        // Act
        var result = await limiter.TryAcquireAsync("key", 10, TimeSpan.FromSeconds(10));

        // Assert
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public void AddRedisRateLimiter_RegistersIRateLimiter()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();

        // Act
        services.AddRedisRateLimiter(redisMock.Object);
        var provider = services.BuildServiceProvider();
        var limiter = provider.GetService<IRateLimiter>();

        // Assert
        limiter.Should().NotBeNull("AddRedisRateLimiter 应注册 IRateLimiter");
        limiter.Should().BeOfType<RedisSlidingWindowRateLimiter>();
    }

    [Fact]
    public void AddRedisRateLimiter_NullServices_Throws()
    {
        var redisMock = new Mock<IConnectionMultiplexer>();
        var act = () => RateLimiterExtensions.AddRedisRateLimiter(null!, redisMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddRedisRateLimiter_NullRedis_Throws()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var act = () => services.AddRedisRateLimiter(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
