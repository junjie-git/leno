using System.ComponentModel.DataAnnotations;

namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 读模型快照持久化实体，映射 <c>read_model_snapshots</c> 表。
/// 主键为 (AggregateId, Version)，保证同一聚合同一版本仅一条快照。
/// <see cref="StateJson"/> 以 JSON 文本存储读模型完整视图，使用 System.Text.Json 序列化。
/// </summary>
public sealed class ReadModelSnapshot
{
    /// <summary>聚合标识。</summary>
    [Key]
    [MaxLength(128)]
    public string AggregateId { get; set; } = string.Empty;

    /// <summary>聚合类型名称，用于按类型列出快照。</summary>
    [Required]
    [MaxLength(128)]
    public string AggregateType { get; set; } = string.Empty;

    /// <summary>快照对应的事件版本号（与 <see cref="AggregateId"/> 联合主键）。</summary>
    [Key]
    public long Version { get; set; }

    /// <summary>读模型完整视图的 JSON 文本。</summary>
    [Required]
    public string StateJson { get; set; } = string.Empty;

    /// <summary>快照生成时间（UTC）。</summary>
    public DateTime TakenAt { get; set; }
}
