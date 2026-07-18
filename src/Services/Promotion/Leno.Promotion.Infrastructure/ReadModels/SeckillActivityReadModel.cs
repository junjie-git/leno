namespace Leno.Promotion.Infrastructure.ReadModels;

/// <summary>
/// 秒杀活动 ES 读模型文档，索引名 <see cref="SeckillActivityIndexName"/>。
/// 用于前台秒杀活动列表与详情页的快速检索。
/// 写侧秒杀活动发布时由 <see cref="SeckillActivityPublishedReadModelSyncConsumer"/> 索引；
/// 活动结束时由 <see cref="SeckillActivityEndedReadModelSyncConsumer"/> 删除。
/// </summary>
public sealed class SeckillActivityReadModel
{
    /// <summary>秒杀活动读模型索引名。</summary>
    public const string SeckillActivityIndexName = "leno_seckill_activities";

    /// <summary>秒杀活动标识，作为 ES 文档 _id。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>关联商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>关联商品 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>原价（用于展示划线价）。</summary>
    public decimal OriginalPrice { get; init; }

    /// <summary>秒杀价。</summary>
    public decimal SeckillPrice { get; init; }

    /// <summary>活动开始时间（UTC）。</summary>
    public DateTime StartTime { get; init; }

    /// <summary>活动结束时间（UTC）。</summary>
    public DateTime EndTime { get; init; }

    /// <summary>活动状态名称（Pending/Active/Ended/Closed）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>总库存。</summary>
    public int TotalStock { get; init; }

    /// <summary>当前可用库存（活动发布时等于 TotalStock，后续由其他事件增量维护）。</summary>
    public int AvailableStock { get; init; }

    /// <summary>索引时间（UTC）。</summary>
    public DateTime IndexedAt { get; init; }

    /// <summary>
    /// 读模型模式版本号，用于后续字段演进时消费方按版本路由反序列化逻辑。
    /// </summary>
    public int SchemaVersion { get; init; } = 1;
}
