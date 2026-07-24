namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 快照记录，承载某聚合在指定版本的读模型完整状态。
/// 重建时作为增量回放的起点，<see cref="Version"/> 之后的事件再回放到 <see cref="State"/> 之上。
/// </summary>
/// <typeparam name="T">读模型类型。</typeparam>
/// <param name="AggregateId">聚合标识。</param>
/// <param name="State">快照状态（读模型完整视图）。</param>
/// <param name="Version">快照对应的事件版本号（该状态已应用截至此版本的全部事件）。</param>
/// <param name="TakenAt">快照生成时间（UTC）。</param>
public sealed record Snapshot<T>(string AggregateId, T State, long Version, DateTime TakenAt) where T : class;
