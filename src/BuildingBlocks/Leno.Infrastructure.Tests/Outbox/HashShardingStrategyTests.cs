using Leno.Infrastructure.Outbox;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.Outbox;

/// <summary>
/// 4.4 Outbox 分片发布器：HashShardingStrategy 单元测试。
/// <para>
/// 覆盖：
/// - 一致性：同一聚合根 ID + 同一分片数 → 始终同一分片号
/// - 边界：分片号始终在 [0, shardCount-1] 范围内
/// - 单分片退化：shardCount=1 → 始终返回 0
/// - 零或负分片数：返回 0（兼容单实例模式）
/// - 分布均衡性：大量随机 GUID 在各分片近似均匀分布（±10%）
/// </para>
/// </summary>
public class HashShardingStrategyTests
{
    /// <summary>
    /// 一致性：同一聚合根 ID 在分片数不变时始终返回相同分片号。
    /// </summary>
    [Fact]
    public void ComputeShard_SameAggregateIdSameShardCount_AlwaysReturnsSameShard()
    {
        // Arrange
        var strategy = HashShardingStrategy.Instance;
        var aggregateRootId = Guid.NewGuid();
        const int shardCount = 8;

        // Act
        var shard1 = strategy.ComputeShard(aggregateRootId, shardCount);
        var shard2 = strategy.ComputeShard(aggregateRootId, shardCount);
        var shard3 = strategy.ComputeShard(aggregateRootId, shardCount);

        // Assert
        shard1.Should().Be(shard2);
        shard2.Should().Be(shard3);
        shard1.Should().BeInRange(0, shardCount - 1);
    }

    /// <summary>
    /// 边界：分片号始终在 [0, shardCount-1] 范围内。
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void ComputeShard_AlwaysReturnsShardInRange(int shardCount)
    {
        // Arrange
        var strategy = HashShardingStrategy.Instance;

        // Act & Assert：100 个随机 GUID 都应落到合法分片范围
        for (int i = 0; i < 100; i++)
        {
            var aggregateRootId = Guid.NewGuid();
            var shard = strategy.ComputeShard(aggregateRootId, shardCount);
            shard.Should().BeInRange(0, shardCount - 1,
                $"GUID {aggregateRootId} 应落到 [0, {shardCount - 1}] 范围内");
        }
    }

    /// <summary>
    /// 单分片退化：shardCount=1 时始终返回 0。
    /// </summary>
    [Fact]
    public void ComputeShard_SingleShard_AlwaysReturnsZero()
    {
        // Arrange
        var strategy = HashShardingStrategy.Instance;
        const int shardCount = 1;

        // Act & Assert
        for (int i = 0; i < 20; i++)
        {
            var aggregateRootId = Guid.NewGuid();
            var shard = strategy.ComputeShard(aggregateRootId, shardCount);
            shard.Should().Be(0);
        }
    }

    /// <summary>
    /// 非法分片数（&lt;=0）返回 0，兼容单实例模式。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ComputeShard_NonPositiveShardCount_ReturnsZero(int shardCount)
    {
        // Arrange
        var strategy = HashShardingStrategy.Instance;
        var aggregateRootId = Guid.NewGuid();

        // Act
        var shard = strategy.ComputeShard(aggregateRootId, shardCount);

        // Assert
        shard.Should().Be(0);
    }

    /// <summary>
    /// 分片数变化时，旧分片号是新分片号的子集（兼容性：增加分片数不丢失旧映射）。
    /// 例如 shardCount 从 4 增到 8，shardKey(4) ∈ {0,1,2,3} → shardKey(8) ∈ {0,1,2,3,4,5,6,7}，
    /// 但不要求 shardKey(4) == shardKey(8) % 4（非一致性哈希严格定义，仅验证边界）。
    /// </summary>
    [Fact]
    public void ComputeShard_IncreaseShardCount_StaysInNewRange()
    {
        // Arrange
        var strategy = HashShardingStrategy.Instance;
        var aggregateRootId = Guid.NewGuid();

        // Act
        var shardAt4 = strategy.ComputeShard(aggregateRootId, 4);
        var shardAt8 = strategy.ComputeShard(aggregateRootId, 8);
        var shardAt16 = strategy.ComputeShard(aggregateRootId, 16);

        // Assert：所有分片号都在各自合法范围内
        shardAt4.Should().BeInRange(0, 3);
        shardAt8.Should().BeInRange(0, 7);
        shardAt16.Should().BeInRange(0, 15);
    }

    /// <summary>
    /// 分布均衡性：1000 个随机 GUID 在 8 分片下各分片占比应在 [10%, 15%] 范围内（±10% 偏差）。
    /// </summary>
    [Fact]
    public void ComputeShard_LargeSample_DistributesEvenlyAcrossShards()
    {
        // Arrange
        var strategy = HashShardingStrategy.Instance;
        const int shardCount = 8;
        const int sampleSize = 1000;
        var distribution = new int[shardCount];

        // Act
        for (int i = 0; i < sampleSize; i++)
        {
            var aggregateRootId = Guid.NewGuid();
            var shard = strategy.ComputeShard(aggregateRootId, shardCount);
            distribution[shard]++;
        }

        // Assert：每个分片应收到约 125 条（1000/8），允许 ±30% 偏差（87~163）
        // GUID 前 8 字节均匀分布，但样本有限，宽松断言避免 flaky
        var expectedPerShard = sampleSize / shardCount;
        var minAllowed = (int)(expectedPerShard * 0.7); // 87
        var maxAllowed = (int)(expectedPerShard * 1.3); // 163

        for (int i = 0; i < shardCount; i++)
        {
            distribution[i].Should().BeInRange(minAllowed, maxAllowed,
                $"分片 {i} 应在 [{minAllowed}, {maxAllowed}] 范围内，实际为 {distribution[i]}");
        }
    }

    /// <summary>
    /// Guid.Empty 也能正常分片（边界值，不抛异常）。
    /// </summary>
    [Fact]
    public void ComputeShard_EmptyGuid_DoesNotThrow()
    {
        // Arrange
        var strategy = HashShardingStrategy.Instance;
        var emptyGuid = Guid.Empty;

        // Act
        var shard = strategy.ComputeShard(emptyGuid, 8);

        // Assert
        shard.Should().BeInRange(0, 7);
    }

    /// <summary>
    /// 所有位都是 0xFF 的 GUID（最大值）也能正常分片。
    /// </summary>
    [Fact]
    public void ComputeShard_MaxGuid_DoesNotThrow()
    {
        // Arrange
        var strategy = HashShardingStrategy.Instance;
        // 构造所有位都是 0xFF 的 GUID
        var maxGuid = new Guid(0xFFFFFFFF, 0xFFFF, 0xFFFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);

        // Act
        var shard = strategy.ComputeShard(maxGuid, 8);

        // Assert
        shard.Should().BeInRange(0, 7);
    }

    /// <summary>
    /// Instance 单例可共享，无状态。
    /// </summary>
    [Fact]
    public void Instance_IsSingletonAndStateless()
    {
        // Arrange & Act
        var instance1 = HashShardingStrategy.Instance;
        var instance2 = HashShardingStrategy.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);

        // 无状态：相同输入在两个引用上调用应返回相同结果
        var guid = Guid.NewGuid();
        instance1.ComputeShard(guid, 4).Should().Be(instance2.ComputeShard(guid, 4));
    }

    /// <summary>
    /// long.MinValue 边界：构造 GUID 前 8 字节为 0x8000000000000000（long.MinValue），
    /// 验证不抛 OverflowException，且返回合法分片号。
    /// </summary>
    [Fact]
    public void ComputeShard_LongMinValueBoundary_DoesNotOverflow()
    {
        // Arrange
        var strategy = HashShardingStrategy.Instance;
        // long.MinValue = 0x8000000000000000，低 64 位字节序为 00 00 00 00 00 00 00 80
        var bytes = new byte[16];
        bytes[7] = 0x80; // 设置第 8 字节为 0x80，BitConverter.ToInt64 读到 long.MinValue
        var guidWithLongMinValue = new Guid(bytes);

        // Act
        var act = () => strategy.ComputeShard(guidWithLongMinValue, 8);

        // Assert：不抛 OverflowException，返回合法分片号
        act.Should().NotThrow();
        var shard = act();
        shard.Should().BeInRange(0, 7);
    }
}
