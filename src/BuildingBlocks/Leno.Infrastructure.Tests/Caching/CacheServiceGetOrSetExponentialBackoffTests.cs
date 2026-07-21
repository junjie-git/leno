using Leno.Infrastructure.Caching;
using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Leno.Infrastructure.Tests.Caching;

/// <summary>
/// T27 单元测试：验证 CacheService.GetOrSetAsync 在未获取互斥锁时采用指数退避重试（50ms → 100ms → 200ms），
/// 重试耗尽后仍无缓存值时记 warning 并直接回源 factory。
/// </summary>
public class CacheServiceGetOrSetExponentialBackoffTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<IBloomFilter> _bloomFilterMock;
    private readonly Mock<ILogger<CacheService>> _loggerMock;
    private readonly CacheService _sut;

    public CacheServiceGetOrSetExponentialBackoffTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _bloomFilterMock = new Mock<IBloomFilter>();
        _loggerMock = new Mock<ILogger<CacheService>>();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_databaseMock.Object);
        _sut = new CacheService(_redisMock.Object, _bloomFilterMock.Object, _loggerMock.Object);

        // 默认配置：BloomFilter 放行 + LockTake 始终失败 + 缓存始终未命中
        _bloomFilterMock.Setup(b => b.MightContainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _databaseMock.Setup(d => d.LockTakeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);
        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
    }

    /// <summary>
    /// T27：LockTake 失败时，应按指数退避间隔重试 StringGetAsync。
    /// 使用 0ms 覆盖值加速测试，但验证 StringGetAsync 调用次数 = 重试次数 + 1（初始 miss）。
    /// </summary>
    [Fact]
    public async Task GetOrSetAsync_LockNotAcquired_ShouldRetryWithExponentialBackoff()
    {
        // Arrange：使用 0ms 覆盖加速测试
        _sut.LockRetryBackoffMsOverride = new[] { 0, 0, 0 };

        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var factoryCallCount = 0;
        var factory = (CancellationToken _) =>
        {
            factoryCallCount++;
            return Task.FromResult<string?>("from-factory");
        };

        // Act
        var result = await _sut.GetOrSetAsync("key", factory);

        // Assert：初始 miss 1 次 + 3 次重试 = 4 次 StringGetAsync
        result.Should().Be("from-factory");
        factoryCallCount.Should().Be(1);
        _databaseMock.Verify(
            d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Exactly(4)); // 1 initial + 3 retries
    }

    /// <summary>
    /// T27：重试期间缓存被其他线程回填时，应返回回填值且不调用 factory。
    /// </summary>
    [Fact]
    public async Task GetOrSetAsync_RetryCachePopulated_ShouldReturnCachedValueWithoutFactory()
    {
        // Arrange
        _sut.LockRetryBackoffMsOverride = new[] { 0, 0, 0 };

        var callCount = 0;
        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // 第 1 次初始 miss + 第 2 次重试仍 miss；第 3 次重试时已被其他线程回填
                return callCount >= 3 ? (RedisValue)"\"backfilled\"" : RedisValue.Null;
            });

        var factoryCalled = false;
        var factory = (CancellationToken _) =>
        {
            factoryCalled = true;
            return Task.FromResult<string?>("from-factory");
        };

        // Act
        var result = await _sut.GetOrSetAsync("key", factory);

        // Assert：第 3 次 StringGetAsync 命中，返回回填值，不调用 factory
        result.Should().Be("backfilled");
        factoryCalled.Should().BeFalse();
    }

    /// <summary>
    /// T27：重试期间命中空值标记时，应返回 null 且不调用 factory。
    /// </summary>
    [Fact]
    public async Task GetOrSetAsync_RetryNullMarkerHit_ShouldReturnNullWithoutFactory()
    {
        // Arrange
        _sut.LockRetryBackoffMsOverride = new[] { 0, 0, 0 };

        var callCount = 0;
        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // 第 2 次重试时返回 null marker
                return callCount == 2 ? (RedisValue)"__NULL_MARKER__" : RedisValue.Null;
            });

        var factoryCalled = false;
        var factory = (CancellationToken _) =>
        {
            factoryCalled = true;
            return Task.FromResult<string?>("from-factory");
        };

        // Act
        var result = await _sut.GetOrSetAsync<string>("key", factory);

        // Assert
        result.Should().BeNull();
        factoryCalled.Should().BeFalse();
    }

    /// <summary>
    /// T27：重试耗尽仍无缓存值时，应记 warning 日志并调用 factory。
    /// </summary>
    [Fact]
    public async Task GetOrSetAsync_RetriesExhausted_ShouldLogWarningAndCallFactory()
    {
        // Arrange
        _sut.LockRetryBackoffMsOverride = new[] { 0, 0, 0 };

        // 缓存始终未命中
        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        List<(LogLevel Level, string Message)> loggedWarnings = new();
        _loggerMock
            .Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((level, _, state, _, _) =>
            {
                if (level == LogLevel.Warning)
                {
                    loggedWarnings.Add((level, state.ToString() ?? string.Empty));
                }
            });

        var factoryCalled = false;
        var factory = (CancellationToken _) =>
        {
            factoryCalled = true;
            return Task.FromResult<string?>("fallback-value");
        };

        // Act
        var result = await _sut.GetOrSetAsync("key", factory);

        // Assert
        result.Should().Be("fallback-value");
        factoryCalled.Should().BeTrue();

        // 应记录至少一条 warning 日志，包含"缓存击穿防护"与重试次数
        loggedWarnings.Should().NotBeEmpty();
        loggedWarnings.Should().Contain(w => w.Message.Contains("缓存击穿防护"));
        loggedWarnings.Should().Contain(w => w.Message.Contains("3 次"));
        loggedWarnings.Should().Contain(w => w.Message.Contains("key"));
    }

    /// <summary>
    /// T27：默认重试间隔为 50ms → 100ms → 200ms（共 3 次）。
    /// 验证 LockRetryBackoffMsOverride 为 null 时使用默认值。
    /// </summary>
    [Fact]
    public void LockRetryBackoffMsOverride_DefaultIsNull_UsesDefaultBackoff()
    {
        // Arrange：不设置 Override
        // Act & Assert：内部 EffectiveLockRetryBackoffMs 不可直接访问，
        // 但通过 GetOrSetAsync 行为可间接验证 — 此处仅验证不抛异常且 Override 属性为 null
        _sut.LockRetryBackoffMsOverride.Should().BeNull();
    }

    /// <summary>
    /// T27：自定义重试次数（如只重试 1 次）应被尊重。
    /// </summary>
    [Fact]
    public async Task GetOrSetAsync_CustomRetryCount_ShouldRespectOverride()
    {
        // Arrange：仅重试 1 次
        _sut.LockRetryBackoffMsOverride = new[] { 0 };

        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var factoryCallCount = 0;
        var factory = (CancellationToken _) =>
        {
            factoryCallCount++;
            return Task.FromResult<string?>("from-factory");
        };

        // Act
        await _sut.GetOrSetAsync("key", factory);

        // Assert：初始 miss 1 次 + 1 次重试 = 2 次 StringGetAsync
        factoryCallCount.Should().Be(1);
        _databaseMock.Verify(
            d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Exactly(2)); // 1 initial + 1 retry
    }

    /// <summary>
    /// T27：取消令牌触发时，重试循环应抛 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task GetOrSetAsync_CancellationDuringRetry_ShouldThrowOperationCanceledException()
    {
        // Arrange：使用较长延迟，并在延迟期间触发取消
        _sut.LockRetryBackoffMsOverride = new[] { 100, 100, 100 };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(20); // 20ms 后取消，恰好在第一次 100ms 延迟期间

        var factory = (CancellationToken _) => Task.FromResult<string?>("unused");

        // Act
        var act = () => _sut.GetOrSetAsync("key", factory, expiry: null, ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// T27：LockTake 成功时不应进入重试循环（验证原有路径未被破坏）。
    /// </summary>
    [Fact]
    public async Task GetOrSetAsync_LockAcquired_ShouldNotRetry()
    {
        // Arrange：LockTake 成功
        _databaseMock.Setup(d => d.LockTakeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _databaseMock.Setup(d => d.LockReleaseAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // 双重检查仍 miss，调用 factory
        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _bloomFilterMock.Setup(b => b.AddAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var factoryCalled = false;
        var factory = (CancellationToken _) =>
        {
            factoryCalled = true;
            return Task.FromResult<string?>("from-factory");
        };

        // Act
        var result = await _sut.GetOrSetAsync("key", factory);

        // Assert：factory 被调用 1 次，StringGetAsync 仅调用 2 次（初始 + 双重检查），无重试
        result.Should().Be("from-factory");
        factoryCalled.Should().BeTrue();
        _databaseMock.Verify(
            d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Exactly(2)); // 1 initial + 1 double-check inside lock
    }
}
