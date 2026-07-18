using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Aggregates;

/// <summary>
/// 价格历史聚合根，记录单个 SKU 每次价格调整的旧价格、新价格与原因。
/// 从 SPU 聚合拆分而来，独立持久化到 price_histories 表，支持按 SPU/SKU 维度查询变更轨迹。
/// 不可变，仅通过工厂方法 <see cref="Create"/> 创建；更新 SKU 价格由 SPU 聚合负责，本聚合仅记录变更事实。
/// </summary>
public sealed class PriceHistory : AggregateRoot
{
    /// <summary>所属 SPU 标识。</summary>
    public Guid SpuId { get; private set; }

    /// <summary>所属 SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>变更前价格。</summary>
    public decimal OldPrice { get; private set; }

    /// <summary>变更后价格，须 ≥ 0。</summary>
    public decimal NewPrice { get; private set; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; private set; } = "CNY";

    /// <summary>变更原因，可空。</summary>
    public string? Reason { get; private set; }

    /// <summary>变更时间（UTC）。</summary>
    public DateTime ChangedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private PriceHistory() { }

    private PriceHistory(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建一条价格变更历史记录。
    /// </summary>
    /// <param name="spuId">所属 SPU 标识。</param>
    /// <param name="skuId">所属 SKU 标识。</param>
    /// <param name="oldPrice">变更前价格。</param>
    /// <param name="newPrice">变更后价格，须 ≥ 0。</param>
    /// <param name="reason">变更原因，可空。</param>
    /// <param name="currency">币种，默认 CNY。</param>
    public static PriceHistory Create(
        Guid spuId,
        Guid skuId,
        decimal oldPrice,
        decimal newPrice,
        string? reason = null,
        string currency = "CNY")
    {
        if (spuId == Guid.Empty)
        {
            throw new ArgumentException("SPU 标识不可为空", nameof(spuId));
        }

        if (skuId == Guid.Empty)
        {
            throw new ArgumentException("SKU 标识不可为空", nameof(skuId));
        }

        if (newPrice < 0)
        {
            throw new ArgumentException("价格不能为负", nameof(newPrice));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("币种不可为空", nameof(currency));
        }

        return new PriceHistory(Guid.NewGuid())
        {
            SpuId = spuId,
            SkuId = skuId,
            OldPrice = oldPrice,
            NewPrice = newPrice,
            Currency = currency,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ChangedAt = DateTime.UtcNow
        };
    }
}
