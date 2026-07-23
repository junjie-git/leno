namespace Leno.SystemAdmin.Infrastructure.Options;

/// <summary>
/// Outbox 归档策略配置（任务 2.4.7）。
/// 通过 <c>appsettings.json</c> 的 <c>SystemAdmin:OutboxArchival</c> 节绑定，
/// 控制已处理 OutboxMessage 的保留期与分批归档大小，避免 <c>outbox_messages</c> 表无限增长。
/// </summary>
public sealed class OutboxArchivalOptions
{
    /// <summary>配置节名称，对应 appsettings.json 中的 <c>SystemAdmin:OutboxArchival</c>。</summary>
    public const string SectionName = "SystemAdmin:OutboxArchival";

    /// <summary>
    /// 已处理 OutboxMessage 的保留天数。
    /// <c>ProcessedAt</c> 早于 <c>UtcNow - RetentionDays</c> 的记录将被归档至 <c>outbox_messages_archive</c> 表后从原表删除。
    /// 默认 7 天，对齐母方案 §5.4 第 4 项 7 天归档策略。
    /// </summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// 单批归档的最大记录数。
    /// 分批处理避免单次事务过长导致锁表与日志膨胀，默认 1000 条/批。
    /// </summary>
    public int BatchSize { get; set; } = 1000;
}
