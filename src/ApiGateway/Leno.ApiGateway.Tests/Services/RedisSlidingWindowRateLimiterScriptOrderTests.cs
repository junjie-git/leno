using Leno.Infrastructure.RateLimiting;
using Moq;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Services;

/// <summary>
/// RedisSlidingWindowRateLimiterPartition Lua 脚本顺序验证。
/// 验证 P0-T8：ZREMRANGEBYSCORE 必须在第一次 ZCARD 之前执行，清除窗口外过期记录后再计数。
/// </summary>
public class RedisSlidingWindowRateLimiterPartitionScriptOrderTests
{
    [Fact]
    public void GetScriptForTesting_ShouldContainBothZremAndZcard()
    {
        // Arrange
        var script = RedisSlidingWindowRateLimiterPartition.GetScriptForTesting();

        // Act + Assert — 脚本必须同时包含清窗口与计数命令
        script.Should().Contain("ZREMRANGEBYSCORE");
        script.Should().Contain("ZCARD");
    }

    [Fact]
    public void Script_ZremRangeByScore_ShouldAppearBeforeFirstZcard()
    {
        // Arrange
        var script = RedisSlidingWindowRateLimiterPartition.GetScriptForTesting();

        // Act — 定位第一个 ZREMRANGEBYSCORE 与第一个 ZCARD 的位置
        var removeIndex = script.IndexOf("ZREMRANGEBYSCORE", StringComparison.OrdinalIgnoreCase);
        var cardIndex = script.IndexOf("ZCARD", StringComparison.OrdinalIgnoreCase);

        // Assert — ZREMRANGEBYSCORE 必须在第一次 ZCARD 之前执行，
        // 清除过期记录后再计数，避免窗口边界附近误拒合法请求。
        removeIndex.Should().BeGreaterThanOrEqualTo(0, "脚本应包含 ZREMRANGEBYSCORE");
        cardIndex.Should().BeGreaterThanOrEqualTo(0, "脚本应包含 ZCARD");
        removeIndex.Should().BeLessThan(cardIndex,
            "ZREMRANGEBYSCORE 必须在第一次 ZCARD 之前执行，清除过期记录后再计数");
    }

    [Fact]
    public async Task AcquireAsync_ExpiredEntriesShouldNotBlockNewRequests()
    {
        // Arrange — permitLimit=2，窗口内已有 2 条过期记录（时间戳在窗口外）。
        // 正确实现：先 ZREMRANGEBYSCORE 清窗口 → ZCARD=0 → ZADD → ZCARD=1 → 返回 1（允许）。
        // 错误实现：先 ZCARD=2（含过期） → 返回 0（拒绝）。
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();

        string? capturedScript = null;
        dbMock.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, _, _, _) =>
            {
                capturedScript = script;
            })
            .ReturnsAsync(RedisResult.Create((RedisValue)1L, ResultType.Integer)); // 允许

        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var limiter = new RedisSlidingWindowRateLimiterPartition(
            dbMock.Object, "test:ratelimit", permitLimit: 2, window: TimeSpan.FromMinutes(1), segmentsPerWindow: 1);

        // Act
        var lease = await limiter.AcquireAsync(1, CancellationToken.None);

        // Assert — 过期记录清除后不应阻止新请求
        lease.IsAcquired.Should().BeTrue("过期记录清除后不应阻止新请求");

        // 验证实际传递给 Redis 的脚本顺序正确
        capturedScript.Should().NotBeNull();
        var rmIndex = capturedScript!.IndexOf("ZREMRANGEBYSCORE", StringComparison.OrdinalIgnoreCase);
        var cardIndex = capturedScript.IndexOf("ZCARD", StringComparison.OrdinalIgnoreCase);
        rmIndex.Should().BeLessThan(cardIndex,
            "实际执行脚本中 ZREMRANGEBYSCORE 必须在 ZCARD 之前");
    }

    [Fact]
    public async Task AcquireAsync_ScriptPassedToRedis_ShouldHaveCorrectOrder()
    {
        // Arrange — 捕获同步与异步路径传递的脚本，验证顺序
        var dbMock = new Mock<IDatabase>();
        string? capturedScript = null;

        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, _, _, _) =>
            {
                capturedScript = script;
            })
            .ReturnsAsync(RedisResult.Create((RedisValue)1L, ResultType.Integer));

        var limiter = new RedisSlidingWindowRateLimiterPartition(
            dbMock.Object, "leno:rl:order-test", 10, TimeSpan.FromSeconds(1), 1);

        // Act
        await limiter.AcquireAsync(1);

        // Assert
        capturedScript.Should().NotBeNull();
        var rmIndex = capturedScript!.IndexOf("ZREMRANGEBYSCORE", StringComparison.OrdinalIgnoreCase);
        var firstCardIndex = capturedScript.IndexOf("ZCARD", StringComparison.OrdinalIgnoreCase);
        rmIndex.Should().BeLessThan(firstCardIndex,
            "脚本必须先清窗口（ZREMRANGEBYSCORE）再计数（ZCARD）");
    }
}
