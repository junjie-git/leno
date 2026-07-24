namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 读模型投影器抽象，负责将领域事件投影到读模型，并支持从快照恢复与查询当前状态。
/// 既支持实时增量投影（每事件触发），也支持快照重建场景下的批量回放。
/// </summary>
/// <typeparam name="TReadModel">读模型类型。</typeparam>
public interface IReadModelProjector<TReadModel> where TReadModel : class
{
    /// <summary>
    /// 将单个领域事件投影到读模型（增量更新），并更新读模型的 Version 字段。
    /// </summary>
    /// <param name="envelope">领域事件信封。</param>
    /// <param name="ct">取消令牌。</param>
    Task ProjectAsync(DomainEventEnvelope envelope, CancellationToken ct);

    /// <summary>
    /// 从快照恢复读模型状态（将快照状态写入读库，作为增量回放的起点）。
    /// </summary>
    /// <param name="snapshot">快照记录。</param>
    /// <param name="ct">取消令牌。</param>
    Task RebuildFromSnapshotAsync(Snapshot<TReadModel> snapshot, CancellationToken ct);

    /// <summary>
    /// 获取指定聚合当前已投影到的最后事件版本号。读模型不存在时返回 0。
    /// </summary>
    /// <param name="aggregateId">聚合标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<long> GetLastProjectedVersionAsync(string aggregateId, CancellationToken ct);

    /// <summary>
    /// 获取指定聚合当前的读模型状态。读模型不存在时返回 null。
    /// </summary>
    /// <param name="aggregateId">聚合标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<TReadModel?> GetCurrentStateAsync(string aggregateId, CancellationToken ct);
}
