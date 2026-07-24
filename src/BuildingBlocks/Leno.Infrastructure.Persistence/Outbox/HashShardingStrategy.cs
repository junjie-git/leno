using System.Runtime.CompilerServices;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// 基于哈希的分片策略实现（4.4 Outbox 分片发布器）。
/// <para>
/// 取聚合根 GUID 的前 8 字节作为 64 位有符号整数，对其取绝对值后对分片数取模，
/// 保证同一聚合根始终落到同一分片（事件顺序性），不同聚合根近似均匀分布（负载均衡）。
/// </para>
/// <para>
/// 实现要点：<br/>
/// - <see cref="Guid.ToByteArray"/> 返回 16 字节数组，取前 8 字节通过
///   <see cref="BitConverter.ToInt64"/> 转为 long，避免 GUID 字符串哈希的碰撞风险；<br/>
/// - <see cref="Math.Abs"/> 处理 long.MinValue 边界（取反仍为负数），通过无条件转换为
///   <see cref="ulong"/> 再取模避免溢出；<br/>
/// - 分片数 &lt;= 1 时返回 0，兼容单实例模式。
/// </para>
/// </summary>
public sealed class HashShardingStrategy : IShardingStrategy
{
    /// <summary>
    /// 无状态策略，可共享单例。
    /// </summary>
    public static readonly HashShardingStrategy Instance = new();

    public int ComputeShard(Guid aggregateRootId, int shardCount)
    {
        if (shardCount <= 1)
        {
            return 0;
        }

        // 取 GUID 前 8 字节作为 long（GUID 结构前 8 字节为低 64 位，分布足够均匀）
        var bytes = aggregateRootId.ToByteArray();
        var hash = BitConverter.ToInt64(bytes, 0);

        // 通过 ulong 强转避免 long.MinValue 取绝对值溢出（Math.Abs(long.MinValue) 仍为负数）
        // 之后对 shardCount 取模保证结果在 [0, shardCount-1] 范围内
        var unsignedHash = Unsafe.As<long, ulong>(ref hash);
        return (int)(unsignedHash % (ulong)shardCount);
    }
}
