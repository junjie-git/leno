namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 增量回放配置选项。
/// </summary>
public sealed class IncrementalReplayOptions
{
    /// <summary>
    /// 快照间隔：每投影 <see cref="SnapshotInterval"/> 个事件生成一次快照。
    /// 默认 100，即每 100 个事件落一个快照。
    /// </summary>
    public int SnapshotInterval { get; set; } = 100;

    /// <summary>
    /// 是否启用快照。禁用时回放退化为全量回放（从版本 0 重建）。
    /// </summary>
    public bool EnableSnapshotting { get; set; } = true;

    /// <summary>
    /// 单次重建超时时间，默认 10 分钟。
    /// </summary>
    public TimeSpan RebuildTimeout { get; set; } = TimeSpan.FromMinutes(10);
}
