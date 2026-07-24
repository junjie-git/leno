namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 快照存储抽象，支持 CQRS 读模型的快照重建与增量回放。
/// 快照按 (AggregateId, Version) 唯一存储，重建时取最新快照后仅回放其后的事件，
/// 相比全量回放显著降低读模型重建耗时。
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// 获取指定聚合的最新快照。不存在时返回 null。
    /// </summary>
    /// <typeparam name="T">读模型类型。</typeparam>
    /// <param name="aggregateId">聚合标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<Snapshot<T>?> GetLatestAsync<T>(string aggregateId, CancellationToken ct) where T : class;

    /// <summary>
    /// 保存指定聚合在 <paramref name="version"/> 版本的快照。
    /// 同 (AggregateId, Version) 已存在时覆盖。
    /// </summary>
    /// <typeparam name="T">读模型类型。</typeparam>
    /// <param name="aggregateId">聚合标识。</param>
    /// <param name="state">快照状态（读模型完整视图）。</param>
    /// <param name="version">快照对应的事件版本号。</param>
    /// <param name="ct">取消令牌。</param>
    Task SaveAsync<T>(string aggregateId, T state, long version, CancellationToken ct) where T : class;

    /// <summary>
    /// 按聚合类型列出所有快照描述符，用于管理后台审计与清理。
    /// </summary>
    /// <param name="aggregateType">聚合类型名称。</param>
    /// <param name="ct">取消令牌。</param>
    Task<IReadOnlyList<SnapshotDescriptor>> ListSnapshotsAsync(string aggregateType, CancellationToken ct);

    /// <summary>
    /// 删除指定聚合在 <paramref name="version"/> 版本的快照。
    /// </summary>
    /// <param name="aggregateId">聚合标识。</param>
    /// <param name="version">快照版本号。</param>
    /// <param name="ct">取消令牌。</param>
    Task DeleteAsync(string aggregateId, long version, CancellationToken ct);
}
