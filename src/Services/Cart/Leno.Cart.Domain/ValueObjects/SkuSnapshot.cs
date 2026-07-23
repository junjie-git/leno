using System.Text.Json.Serialization;

namespace Leno.Cart.Domain.ValueObjects;

/// <summary>
/// SKU 快照值对象，购物车本地缓存的商品域 SKU 展示与价格信息。
/// <para>
/// 阶段三 3.11：Cart 聚合本地化存储 SKU 快照（名称/价格/主图/规格/可售状态），
/// 消除 <c>CartPriceService</c> 读取路径上的实时跨进程调用。
/// 快照由 <c>ProductSkuUpdatedEvent</c> 消费者异步刷新，或快照过期时由后台刷新队列拉取最新数据。
/// </para>
/// <para>
/// 作为 EF Core owned entity 映射到 <c>cart_items</c> 表的 <c>sku_snapshot_*</c> 列，
/// 随 CartItem 所属聚合一并持久化。所有列可空，允许历史数据渐进回填。
/// </para>
/// </summary>
public sealed record SkuSnapshot
{
    /// <summary>商品 SKU 标识（与所属 CartItem.SkuId 一致）。</summary>
    public Guid SkuId { get; init; }

    /// <summary>商品标题（用于购物车展示，对应 <see cref="Leno.Cart.Domain.Services.SkuPriceSnapshot.Title"/>）。</summary>
    public string SkuName { get; init; } = string.Empty;

    /// <summary>SKU 单价。</summary>
    public decimal Price { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>主图 URL（用于购物车展示）。</summary>
    public string? MainImageUrl { get; init; }

    /// <summary>规格文本（如"红色 / XL"，用于购物车展示）。</summary>
    public string? SpecText { get; init; }

    /// <summary>是否可售（在售且有库存）。</summary>
    public bool Available { get; init; }

    /// <summary>快照版本号，每次刷新递增，用于并发冲突检测。</summary>
    public int SnapshotVersion { get; init; }

    /// <summary>快照时间（UTC），过期阈值由 <see cref="IsStale(TimeSpan)"/> 判定。</summary>
    public DateTime SnapshotAt { get; init; }

    /// <summary>
    /// EF Core 物化用的无参构造；同时作为 System.Text.Json 反序列化入口，
    /// 与 <see cref="Leno.Cart.Domain.Aggregates.CartItem"/> 反序列化策略保持一致。
    /// </summary>
    [JsonConstructor]
    private SkuSnapshot() { }

    /// <summary>创建 SKU 快照。</summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="skuName">商品标题。</param>
    /// <param name="price">单价。</param>
    /// <param name="currency">币种。</param>
    /// <param name="mainImageUrl">主图 URL。</param>
    /// <param name="specText">规格文本。</param>
    /// <param name="available">是否可售。</param>
    /// <param name="snapshotVersion">快照版本号。</param>
    /// <param name="snapshotAt">快照时间（UTC）。</param>
    public SkuSnapshot(
        Guid skuId,
        string skuName,
        decimal price,
        string currency,
        string? mainImageUrl,
        string? specText,
        bool available,
        int snapshotVersion,
        DateTime snapshotAt)
    {
        if (skuId == Guid.Empty)
        {
            throw new ArgumentException("SkuId 不可为空", nameof(skuId));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency 不可为空", nameof(currency));
        }

        SkuId = skuId;
        SkuName = skuName ?? string.Empty;
        Price = price;
        Currency = currency;
        MainImageUrl = mainImageUrl;
        SpecText = specText;
        Available = available;
        SnapshotVersion = snapshotVersion < 0 ? 0 : snapshotVersion;
        SnapshotAt = snapshotAt;
    }

    /// <summary>
    /// 判断快照是否过期。
    /// 过期判定基于 <see cref="SnapshotAt"/> 与当前 UTC 时间的差值是否超过 <paramref name="maxAge"/>。
    /// </summary>
    /// <param name="maxAge">最大允许过期时长。</param>
    /// <returns>true 表示快照已过期需刷新；false 表示快照仍有效。</returns>
    public bool IsStale(TimeSpan maxAge) => DateTime.UtcNow - SnapshotAt > maxAge;

    /// <summary>
    /// 基于当前快照生成下一版本（SnapshotVersion + 1，SnapshotAt 更新为当前 UTC 时间）。
    /// 用于后台刷新与事件消费者刷新快照时构造新版本。
    /// </summary>
    /// <param name="snapshotAt">刷新时间；默认 <see cref="DateTime.UtcNow"/>。</param>
    /// <returns>递增版本号后的新快照。</returns>
    public SkuSnapshot NextVersion(DateTime? snapshotAt = null) => this with
    {
        SnapshotVersion = SnapshotVersion + 1,
        SnapshotAt = snapshotAt ?? DateTime.UtcNow
    };
}
