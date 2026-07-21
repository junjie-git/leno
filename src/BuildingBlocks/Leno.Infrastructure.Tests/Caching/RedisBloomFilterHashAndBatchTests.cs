using Leno.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Leno.Infrastructure.Tests.Caching;

/// <summary>
/// RedisBloomFilter 哈希优化与批量写入修复验证。
/// T37：SHA256 加密哈希 → XxHash64 非加密哈希（~10x 性能提升）。
/// T38：AddAsync 从 N 次 StringSetBitAsync 循环 → 单次 Lua 脚本批量设置（1 次网络往返）。
/// </summary>
public class RedisBloomFilterHashAndBatchTests
{
    private static RedisBloomFilter CreateFilter(out Mock<IDatabase> dbMock)
    {
        var redisMock = new Mock<IConnectionMultiplexer>();
        dbMock = new Mock<IDatabase>();
        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        var loggerMock = new Mock<ILogger<RedisBloomFilter>>();

        return new RedisBloomFilter(
            redisMock.Object,
            loggerMock.Object,
            "test:bloom",
            expectedElements: 100,
            falsePositiveRate: 0.01);
    }

    private static MethodInfo GetGetHashPositionsMethod()
    {
        return typeof(RedisBloomFilter).GetMethod("GetHashPositions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    // === T37: XxHash64 哈希验证 ===

    [Fact]
    public void GetHashPositions_WithXxHash64_ShouldProduceNonNegativePositions()
    {
        // Arrange
        var filter = CreateFilter(out _);
        var method = GetGetHashPositionsMethod();

        // Act — 大量 key 验证 XxHash64 不产生负位置
        var negativeCount = 0;
        for (var i = 0; i < 50000; i++)
        {
            var key = $"xxhash-test-{i}-{'\x00'}-{'\xFF'}";
            var positions = (long[])method.Invoke(filter, new object[] { key })!;

            foreach (var pos in positions)
            {
                if (pos < 0) negativeCount++;
            }
        }

        // Assert
        negativeCount.Should().Be(0,
            "XxHash64 + 位掩码修复应保证所有位置非负");
    }

    [Fact]
    public void GetHashPositions_WithXxHash64_ShouldBeDeterministic()
    {
        // Arrange
        var filter = CreateFilter(out _);
        var method = GetGetHashPositionsMethod();

        // Act — 同一 key 两次调用应产生相同位置
        var key = "deterministic-test-key";
        var positions1 = (long[])method.Invoke(filter, new object[] { key })!;
        var positions2 = (long[])method.Invoke(filter, new object[] { key })!;

        // Assert
        positions1.Should().Equal(positions2,
            "XxHash64 是确定性哈希，同一 key 应产生相同位置");
    }

    [Fact]
    public void GetHashPositions_WithXxHash64_ShouldProduceVariedPositionsForDifferentKeys()
    {
        // Arrange
        var filter = CreateFilter(out _);
        var method = GetGetHashPositionsMethod();

        // Act — 不同 key 应产生不同的位置集合
        var positionSets = new HashSet<string>();
        for (var i = 0; i < 1000; i++)
        {
            var key = $"distribution-test-{i}";
            var positions = (long[])method.Invoke(filter, new object[] { key })!;
            var signature = string.Join(",", positions.OrderBy(p => p));
            positionSets.Add(signature);
        }

        // Assert — 1000 个不同 key 应产生大量不同签名（允许极少量碰撞）
        positionSets.Count.Should().BeGreaterThan(900,
            "XxHash64 应为不同 key 产生足够分散的位置签名");
    }

    [Fact]
    public void GetHashPositions_WithXxHash64_AllPositionsShouldBeWithinBitSize()
    {
        // Arrange
        var filter = CreateFilter(out _);
        var method = GetGetHashPositionsMethod();
        var bitSizeField = typeof(RedisBloomFilter).GetField("_bitSize",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var bitSize = (int)bitSizeField!.GetValue(filter)!;

        // Act + Assert
        for (var i = 0; i < 10000; i++)
        {
            var key = $"range-xxhash-{i}";
            var positions = (long[])method.Invoke(filter, new object[] { key })!;

            foreach (var pos in positions)
            {
                pos.Should().BeInRange(0, bitSize - 1,
                    $"位置应在 [0, {bitSize - 1}] 范围内");
            }
        }
    }

    // === T38: Lua 批量写入验证 ===

    [Fact]
    public async Task AddAsync_ShouldUseSingleLuaScriptEvaluate_NotMultipleStringSetBit()
    {
        // Arrange
        var filter = CreateFilter(out var dbMock);

        // Act
        await filter.AddAsync("batch-test-key");

        // Assert — 应调用 ScriptEvaluateAsync 一次，不调用 StringSetBitAsync
        dbMock.Verify(
            x => x.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Once,
            "AddAsync 应使用单次 Lua 脚本批量设置 bit");

        dbMock.Verify(
            x => x.StringSetBitAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()),
            Times.Never,
            "AddAsync 不应再调用 StringSetBitAsync（已改为 Lua 批量）");
    }

    [Fact]
    public async Task AddAsync_LuaScriptShouldContainAllHashPositions()
    {
        // Arrange
        var filter = CreateFilter(out var dbMock);
        var method = GetGetHashPositionsMethod();
        var expectedPositions = (long[])method.Invoke(filter, new object[] { "arg-count-test" })!;

        RedisValue[]? capturedArgs = null;
        dbMock.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, _, args, _) => capturedArgs = args)
            .ReturnsAsync(RedisResult.Create(expectedPositions.Length));

        // Act
        await filter.AddAsync("arg-count-test");

        // Assert — Lua 脚本的 ARGV 数量应等于哈希位置数量
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Length.Should().Be(expectedPositions.Length,
            "Lua 脚本 ARGV 数量应等于哈希位置数量（每个位置一个 SETBIT）");

        // 验证 ARGV 值与哈希位置一致
        for (var i = 0; i < expectedPositions.Length; i++)
        {
            ((long)capturedArgs[i]).Should().Be(expectedPositions[i],
                $"ARGV[{i + 1}] 应等于第 {i} 个哈希位置");
        }
    }

    [Fact]
    public async Task AddAsync_LuaScriptShouldUseCorrectRedisKey()
    {
        // Arrange
        var filter = CreateFilter(out var dbMock);

        RedisKey[]? capturedKeys = null;
        dbMock.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, keys, _, _) => capturedKeys = keys)
            .ReturnsAsync(RedisResult.Create(7));

        // Act
        await filter.AddAsync("redis-key-test");

        // Assert — Lua 脚本的 KEYS[1] 应为 _redisKey
        capturedKeys.Should().NotBeNull();
        capturedKeys!.Length.Should().Be(1);
        capturedKeys[0].ToString().Should().Be("test:bloom",
            "Lua 脚本 KEYS[1] 应为布隆过滤器的 Redis key");
    }

    [Fact]
    public async Task AddAsync_NullKey_ShouldThrow()
    {
        // Arrange
        var filter = CreateFilter(out _);

        // Act
        var act = () => filter.AddAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddAsync_MultipleKeys_ShouldCallScriptEvaluateEachTime()
    {
        // Arrange
        var filter = CreateFilter(out var dbMock);

        // Act — 添加多个 key
        await filter.AddAsync("key-1");
        await filter.AddAsync("key-2");
        await filter.AddAsync("key-3");

        // Assert — 每次 AddAsync 调用一次 ScriptEvaluateAsync
        dbMock.Verify(
            x => x.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Exactly(3),
            "每次 AddAsync 应调用一次 ScriptEvaluateAsync");
    }
}
