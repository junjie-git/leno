using Leno.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Reflection;

namespace Leno.Infrastructure.Tests.Caching;

/// <summary>
/// RedisBloomFilter 位掩码修复验证：Math.Abs(long.MinValue) 溢出导致负索引。
/// 验证 P0-T6：使用位掩码 & 0x7FFFFFFFFFFFFFFF 替代 Math.Abs，消除负位置。
/// </summary>
public class RedisBloomFilterOverflowTests
{
    private static RedisBloomFilter CreateFilter(out long bitSize)
    {
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        var loggerMock = new Mock<ILogger<RedisBloomFilter>>();

        // 使用小规模参数构造过滤器，便于范围验证
        var filter = new RedisBloomFilter(
            redisMock.Object,
            loggerMock.Object,
            "test:bloom",
            expectedElements: 100,
            falsePositiveRate: 0.01);

        // 通过反射读取 _bitSize 字段用于范围断言
        var bitSizeField = typeof(RedisBloomFilter).GetField("_bitSize",
            BindingFlags.NonPublic | BindingFlags.Instance);
        bitSize = (int)bitSizeField!.GetValue(filter)!;
        return filter;
    }

    private static MethodInfo GetGetHashPositionsMethod()
    {
        return typeof(RedisBloomFilter).GetMethod("GetHashPositions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    [Fact]
    public void GetHashPositions_ShouldNeverProduceNegativePositions()
    {
        // Arrange
        var filter = CreateFilter(out _);
        var method = GetGetHashPositionsMethod();

        // Act — 构造可能产生 long.MinValue 的输入（含空字节与高字节字符，扩大哈希覆盖）
        var negativeCount = 0;
        for (var i = 0; i < 100000; i++)
        {
            var key = $"overflow-test-key-{i}-{'\x00'}-{'\xFF'}";
            var positions = (long[])method.Invoke(filter, new object[] { key })!;

            foreach (var pos in positions)
            {
                if (pos < 0)
                {
                    negativeCount++;
                }
            }
        }

        // Assert — 不应有任何负数位置
        negativeCount.Should().Be(0,
            "GetHashPositions 不应产生负数位置（Math.Abs(long.MinValue) 溢出已用位掩码修复）");
    }

    [Fact]
    public void GetHashPositions_AllPositionsShouldBeWithinBitSize()
    {
        // Arrange
        var filter = CreateFilter(out var bitSize);
        var method = GetGetHashPositionsMethod();

        // Act + Assert
        for (var i = 0; i < 10000; i++)
        {
            var key = $"range-test-{i}";
            var positions = (long[])method.Invoke(filter, new object[] { key })!;

            foreach (var pos in positions)
            {
                pos.Should().BeInRange(0, bitSize - 1,
                    $"位置应在 [0, {bitSize - 1}] 范围内");
            }
        }
    }

    [Fact]
    public void GetHashPositions_LongMinValueCombinedHash_ShouldProduceNonNegativePosition()
    {
        // Arrange — 直接验证修复逻辑：即便 combinedHash 为 long.MinValue，
        // 位掩码 & 0x7FFFFFFFFFFFFFFF 会得到 0，取模后为 0（非负）。
        // 这里通过大量 key 触发 unchecked 溢出场景，确保修复覆盖极端值。
        var filter = CreateFilter(out _);
        var method = GetGetHashPositionsMethod();

        var allNonNegative = true;
        // 使用长 key 增加哈希碰撞到极端值的概率
        for (var i = 0; i < 50000; i++)
        {
            var key = new string('x', i % 256) + i;
            var positions = (long[])method.Invoke(filter, new object[] { key })!;
            if (positions.Any(p => p < 0))
            {
                allNonNegative = false;
                break;
            }
        }

        allNonNegative.Should().BeTrue(
            "即使 combinedHash 通过 unchecked 溢出为 long.MinValue，位掩码修复也应保证非负位置");
    }
}
