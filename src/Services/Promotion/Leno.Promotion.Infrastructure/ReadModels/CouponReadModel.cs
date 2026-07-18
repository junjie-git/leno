namespace Leno.Promotion.Infrastructure.ReadModels;

/// <summary>
/// 优惠券 ES 读模型文档，索引名 <see cref="CouponIndexName"/>。
/// 用于用户端优惠券列表与领券中心快速检索。
/// 写侧券模板创建时由 <see cref="CouponCreatedReadModelSyncConsumer"/> 索引；
/// 停用时由 <see cref="CouponDisabledReadModelSyncConsumer"/> 删除。
/// </summary>
public sealed class CouponReadModel
{
    /// <summary>优惠券读模型索引名。</summary>
    public const string CouponIndexName = "leno_coupons";

    /// <summary>优惠券模板标识，作为 ES 文档 _id。</summary>
    public Guid CouponId { get; init; }

    /// <summary>券名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>券类型名称（FixedAmount/Percentage/FullReduction）。</summary>
    public string CouponType { get; init; } = string.Empty;

    /// <summary>面值（金额或折扣率）。</summary>
    public decimal FaceValue { get; init; }

    /// <summary>使用门槛（满 MinSpend 方可用券），0 表示无门槛。</summary>
    public decimal MinSpend { get; init; }

    /// <summary>固定时段有效期起始（UTC，可空表示相对天数类型）。</summary>
    public DateTime? ValidFrom { get; init; }

    /// <summary>固定时段有效期截止（UTC，可空表示相对天数类型）。</summary>
    public DateTime? ValidTo { get; init; }

    /// <summary>发放总量，-1 表示不限量。</summary>
    public int TotalQty { get; init; }

    /// <summary>已发放数量（创建时为 0，后续由其他事件增量维护）。</summary>
    public int IssuedQty { get; init; }

    /// <summary>券模板状态名称（Enabled/Disabled）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>索引时间（UTC）。</summary>
    public DateTime IndexedAt { get; init; }

    /// <summary>
    /// 读模型模式版本号，用于后续字段演进时消费方按版本路由反序列化逻辑。
    /// </summary>
    public int SchemaVersion { get; init; } = 1;
}
