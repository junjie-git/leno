using Leno.Infrastructure.RateLimiting;
using Moq;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Services;

public class RedisSlidingWindowRateLimiterPartitionTests
{
    private static Mock<IDatabase> CreateDatabaseMock(long scriptResult)
    {
        var mock = new Mock<IDatabase>();
        mock.Setup(d => d.ScriptEvaluate(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Returns(RedisResult.Create((RedisValue)scriptResult, ResultType.Integer));

        mock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)scriptResult, ResultType.Integer));
        return mock;
    }

    [Fact]
    public async Task AcquireAsync_WhenRedisReturnsOne_GrantsLease()
    {
        // Arrange — Lua 脚本返回 1 表示允许
        var dbMock = CreateDatabaseMock(scriptResult: 1);
        var limiter = new RedisSlidingWindowRateLimiterPartition(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act
        var lease = await limiter.AcquireAsync(1);

        // Assert
        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_WhenRedisReturnsZero_DeniesLease()
    {
        // Arrange — Lua 脚本返回 0 表示拒绝
        var dbMock = CreateDatabaseMock(scriptResult: 0);
        var limiter = new RedisSlidingWindowRateLimiterPartition(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act
        var lease = await limiter.AcquireAsync(1);

        // Assert
        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task AcquireAsync_WhenRedisThrows_FailsOpenAndGrantsLease()
    {
        // Arrange — Redis 异常时降级放行，避免 Redis 故障阻断所有流量
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        var limiter = new RedisSlidingWindowRateLimiterPartition(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act
        var lease = await limiter.AcquireAsync(1);

        // Assert — fail-open：Redis 不可用时放行
        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_WhenCancelled_PropagatesCancellation()
    {
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new OperationCanceledException());

        var limiter = new RedisSlidingWindowRateLimiterPartition(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await limiter.AcquireAsync(1, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullDatabase_Throws()
    {
        var act = () => new RedisSlidingWindowRateLimiterPartition(null!, "key", 50, TimeSpan.FromSeconds(1), 4);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Constructor_EmptyOrNullKey_Throws(string? key)
    {
        var dbMock = new Mock<IDatabase>();
        var act = () => new RedisSlidingWindowRateLimiterPartition(dbMock.Object, key!, 50, TimeSpan.FromSeconds(1), 4);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositivePermitLimit_Throws(int limit)
    {
        var dbMock = new Mock<IDatabase>();
        var act = () => new RedisSlidingWindowRateLimiterPartition(dbMock.Object, "key", limit, TimeSpan.FromSeconds(1), 4);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task AcquireAsync_PassesCorrectKeyAndArgsToRedis()
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

        var limiter = new RedisSlidingWindowRateLimiterPartition(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act
        await limiter.AcquireAsync(1);

        // Assert
        capturedKeys.Should().NotBeNull();
        capturedKeys![0].ToString().Should().Be("leno:rl:seckill:user-1");

        capturedValues.Should().NotBeNull();
        capturedValues!.Length.Should().Be(5);
        // ARGV[4] = permit limit
        ((long)capturedValues[3]).Should().Be(50);
        // ARGV[5] = TTL seconds (1.1s 留 10% 余量 → 2s)
        ((long)capturedValues[4]).Should().Be(2);
    }

    [Fact]
    public void AttemptAcquire_SyncPath_AlsoWorks()
    {
        // Arrange
        var dbMock = CreateDatabaseMock(scriptResult: 1);
        var limiter = new RedisSlidingWindowRateLimiterPartition(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act — 同步路径
        var lease = limiter.AttemptAcquire(1);

        // Assert
        lease.IsAcquired.Should().BeTrue();
    }
}
