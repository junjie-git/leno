using Leno.Infrastructure.Caching;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Leno.Infrastructure.Tests.Caching;

public class RedisBloomFilterTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<ILogger<RedisBloomFilter>> _loggerMock;
    private readonly RedisBloomFilter _sut;

    public RedisBloomFilterTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisBloomFilter>>();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_databaseMock.Object);
        _sut = new RedisBloomFilter(_redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task AddAsync_ValidKey_ShouldSetBits()
    {
        _databaseMock
            .Setup(d => d.StringSetBitAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), true, It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var act = () => _sut.AddAsync("test-key");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AddAsync_NullKey_ShouldThrow()
    {
        var act = () => _sut.AddAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task MightContainAsync_AllBitsSet_ShouldReturnTrue()
    {
        _databaseMock
            .Setup(d => d.StringGetBitAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _sut.MightContainAsync("test-key");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task MightContainAsync_SomeBitNotSet_ShouldReturnFalse()
    {
        var callCount = 0;
        _databaseMock
            .Setup(d => d.StringGetBitAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount != 3; // 第3个调用返回 false
            });

        var result = await _sut.MightContainAsync("test-key");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MightContainAsync_NullKey_ShouldThrow()
    {
        var act = () => _sut.MightContainAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddAndCheck_ShouldReturnTrue_ForSameKey()
    {
        // 使用真实的位设置/获取逻辑测试
        var bitsSet = new HashSet<long>();
        _databaseMock
            .Setup(d => d.StringSetBitAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), true, It.IsAny<CommandFlags>()))
            .Callback<RedisKey, long, bool, CommandFlags>((_, pos, _, _) => bitsSet.Add(pos))
            .ReturnsAsync(true);

        _databaseMock
            .Setup(d => d.StringGetBitAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey _, long pos, CommandFlags _) => bitsSet.Contains(pos));

        await _sut.AddAsync("consistent-key");
        var result = await _sut.MightContainAsync("consistent-key");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DifferentKey_ShouldNotBeContained_WhenNotAdded()
    {
        var bitsSet = new HashSet<long>();
        _databaseMock
            .Setup(d => d.StringSetBitAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), true, It.IsAny<CommandFlags>()))
            .Callback<RedisKey, long, bool, CommandFlags>((_, pos, _, _) => bitsSet.Add(pos))
            .ReturnsAsync(true);

        _databaseMock
            .Setup(d => d.StringGetBitAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey _, long pos, CommandFlags _) => bitsSet.Contains(pos));

        await _sut.AddAsync("key-a");
        var result = await _sut.MightContainAsync("key-b");

        // 对于未添加的 key，在真实布隆过滤器中可能返回 false（大概率）
        // 这里我们只验证不抛异常
        result.Should().BeFalse();
    }
}

public class CacheServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<IBloomFilter> _bloomFilterMock;
    private readonly Mock<ILogger<CacheService>> _loggerMock;
    private readonly CacheService _sut;

    public CacheServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _bloomFilterMock = new Mock<IBloomFilter>();
        _loggerMock = new Mock<ILogger<CacheService>>();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_databaseMock.Object);
        _sut = new CacheService(_redisMock.Object, _bloomFilterMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetOrSetAsync_BloomFilterSaysNo_ShouldReturnNull()
    {
        _bloomFilterMock.Setup(b => b.MightContainAsync("key", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.GetOrSetAsync<string>("key", _ => Task.FromResult<string?>("value"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrSetAsync_CacheHit_ShouldReturnCachedValue()
    {
        _bloomFilterMock.Setup(b => b.MightContainAsync("key", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _databaseMock.Setup(d => d.StringGetAsync("key", It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"\"cached_value\"");

        var result = await _sut.GetOrSetAsync<string>("key", _ => Task.FromResult<string?>("factory_value"));

        result.Should().Be("cached_value");
    }

    [Fact]
    public async Task GetOrSetAsync_NullMarker_ShouldReturnNull()
    {
        _bloomFilterMock.Setup(b => b.MightContainAsync("key", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _databaseMock.Setup(d => d.StringGetAsync("key", It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"__NULL_MARKER__");

        var result = await _sut.GetOrSetAsync<string>("key", _ => Task.FromResult<string?>("factory_value"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrSetAsync_CacheMiss_ShouldCallFactory()
    {
        _bloomFilterMock.Setup(b => b.MightContainAsync("key", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _databaseMock.Setup(d => d.StringGetAsync("key", It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);
        _databaseMock.Setup(d => d.LockTakeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _databaseMock.Setup(d => d.LockReleaseAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _bloomFilterMock.Setup(b => b.AddAsync("key", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var factoryCalled = false;
        var result = await _sut.GetOrSetAsync("key", _ =>
        {
            factoryCalled = true;
            return Task.FromResult<string?>("factory_value");
        });

        factoryCalled.Should().BeTrue();
        result.Should().Be("factory_value");
    }

    [Fact]
    public async Task GetOrSetAsync_FactoryReturnsNull_ShouldCacheNullMarker()
    {
        _bloomFilterMock.Setup(b => b.MightContainAsync("key", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _databaseMock.Setup(d => d.StringGetAsync("key", It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);
        _databaseMock.Setup(d => d.LockTakeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _databaseMock.Setup(d => d.LockReleaseAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _bloomFilterMock.Setup(b => b.AddAsync("key", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.GetOrSetAsync<string>("key", _ => Task.FromResult<string?>(null));

        result.Should().BeNull();
        _databaseMock.Verify(d => d.StringSetAsync("key", "__NULL_MARKER__", It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_NullKey_ShouldThrow()
    {
        var act = () => _sut.GetOrSetAsync<string>(null!, _ => Task.FromResult<string?>("value"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetAsync_Valid_ShouldAddToBloomFilter()
    {
        _bloomFilterMock.Setup(b => b.AddAsync("key", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var act = () => _sut.SetAsync("key", "value");

        await act.Should().NotThrowAsync();
        _bloomFilterMock.Verify(b => b.AddAsync("key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_NullKey_ShouldThrow()
    {
        var act = () => _sut.SetAsync(null!, "value");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetAsync_NullValue_ShouldThrow()
    {
        var act = () => _sut.SetAsync<string>("key", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RemoveAsync_Valid_ShouldDeleteKey()
    {
        _databaseMock.Setup(d => d.KeyDeleteAsync("key", It.IsAny<CommandFlags>())).ReturnsAsync(true);

        var act = () => _sut.RemoveAsync("key");

        await act.Should().NotThrowAsync();
        _databaseMock.Verify(d => d.KeyDeleteAsync("key", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task PreWarmBloomFilterAsync_ShouldAddAllKeys()
    {
        _bloomFilterMock.Setup(b => b.AddAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var keys = new[] { "key1", "key2", "key3" };
        await _sut.PreWarmBloomFilterAsync(keys);

        _bloomFilterMock.Verify(b => b.AddAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task PreWarmBloomFilterAsync_NullKeys_ShouldThrow()
    {
        var act = () => _sut.PreWarmBloomFilterAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void ApplyJitter_ShouldAddRandomTimeWithinRange()
    {
        var baseExpiry = TimeSpan.FromMinutes(5);
        var result = _sut.ApplyJitter(baseExpiry);

        result.Should().BeGreaterThan(baseExpiry);
        result.Should().BeLessThanOrEqualTo(baseExpiry.Add(TimeSpan.FromSeconds(120)));
    }

    [Fact]
    public async Task GetAsync_CacheHit_ShouldReturnValue()
    {
        _databaseMock.Setup(d => d.StringGetAsync("key", It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"\"test_value\"");

        var result = await _sut.GetAsync<string>("key");

        result.Should().Be("test_value");
    }

    [Fact]
    public async Task GetAsync_CacheMiss_ShouldReturnNull()
    {
        _databaseMock.Setup(d => d.StringGetAsync("key", It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);

        var result = await _sut.GetAsync<string>("key");

        result.Should().BeNull();
    }

    // ===== T21.2: 双删模式（InvalidateWithDoubleDeleteAsync）测试 =====

    [Fact]
    public async Task InvalidateWithDoubleDelete_NullKey_ShouldThrow()
    {
        var act = () => _sut.InvalidateWithDoubleDeleteAsync(null!, _ => Task.CompletedTask);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InvalidateWithDoubleDelete_NullWriteAction_ShouldThrow()
    {
        var act = () => _sut.InvalidateWithDoubleDeleteAsync("key", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// T21.2：双删模式应按序执行——第一次删除 → 写库 → 延迟 → 第二次删除。
    /// 使用短延迟覆盖避免测试等待 500ms。
    /// </summary>
    [Fact]
    public async Task InvalidateWithDoubleDelete_ShouldDeleteThenWriteThenDeleteAgain()
    {
        // Arrange：使用短延迟覆盖加速测试
        _sut.DoubleDeleteDelayOverride = TimeSpan.FromMilliseconds(1);
        _databaseMock.Setup(d => d.KeyDeleteAsync("key", It.IsAny<CommandFlags>())).ReturnsAsync(true);

        var callSequence = new List<string>();
        _databaseMock
            .Setup(d => d.KeyDeleteAsync("key", It.IsAny<CommandFlags>()))
            .Callback(() => callSequence.Add("delete"))
            .ReturnsAsync(true);

        var writeCalled = false;
        var writeAction = (CancellationToken _) =>
        {
            callSequence.Add("write");
            writeCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await _sut.InvalidateWithDoubleDeleteAsync("key", writeAction);

        // Assert：调用顺序为 delete → write → delete
        callSequence.Should().Equal("delete", "write", "delete");
        writeCalled.Should().BeTrue();
        _databaseMock.Verify(d => d.KeyDeleteAsync("key", It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    /// <summary>
    /// T21.2：写库失败时，finally 块仍应执行第二次删除，避免脏缓存残留。
    /// </summary>
    [Fact]
    public async Task InvalidateWithDoubleDelete_WriteFails_ShouldStillExecuteSecondDelete()
    {
        // Arrange
        _sut.DoubleDeleteDelayOverride = TimeSpan.FromMilliseconds(1);
        _databaseMock.Setup(d => d.KeyDeleteAsync("key", It.IsAny<CommandFlags>())).ReturnsAsync(true);

        var writeAction = (CancellationToken _) => Task.FromException(new InvalidOperationException("DB 写入失败"));

        // Act
        var act = () => _sut.InvalidateWithDoubleDeleteAsync("key", writeAction);

        // Assert：写库异常向上抛出
        await act.Should().ThrowAsync<InvalidOperationException>();
        // 第二次删除仍应执行（finally 块）
        _databaseMock.Verify(d => d.KeyDeleteAsync("key", It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    /// <summary>
    /// T21.2：第二次删除失败不应影响已抛出的写库异常，且不向上抛出。
    /// </summary>
    [Fact]
    public async Task InvalidateWithDoubleDelete_SecondDeleteFails_ShouldNotMaskWriteException()
    {
        // Arrange
        _sut.DoubleDeleteDelayOverride = TimeSpan.FromMilliseconds(1);
        var deleteCallCount = 0;
        // 第一次删除成功，第二次删除抛异常
        _databaseMock
            .Setup(d => d.KeyDeleteAsync("key", It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                deleteCallCount++;
                if (deleteCallCount == 2)
                {
                    throw new RedisException("第二次删除失败");
                }
                return true;
            });

        var writeAction = (CancellationToken _) => Task.FromException(new InvalidOperationException("DB 写入失败"));

        // Act：应抛出写库异常（而非第二次删除异常，因第二次删除异常在 finally 内被吞）
        var act = () => _sut.InvalidateWithDoubleDeleteAsync("key", writeAction);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("DB 写入失败");
        _databaseMock.Verify(d => d.KeyDeleteAsync("key", It.IsAny<CommandFlags>()), Times.Exactly(2));
    }
}