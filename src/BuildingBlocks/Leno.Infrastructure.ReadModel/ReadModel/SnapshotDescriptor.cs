namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 快照描述符，仅包含快照元数据（不含状态体），用于管理后台列表与审计。
/// </summary>
/// <param name="AggregateId">聚合标识。</param>
/// <param name="AggregateType">聚合类型名称。</param>
/// <param name="Version">快照版本号。</param>
/// <param name="TakenAt">快照生成时间（UTC）。</param>
public sealed record SnapshotDescriptor(string AggregateId, string AggregateType, long Version, DateTime TakenAt);
