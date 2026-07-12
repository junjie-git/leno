namespace Leno.Product.Domain.ValueObjects;

/// <summary>
/// 价格变更记录值对象，记录每次 SKU 价格调整的旧价格、新价格、变更人与变更时间。
/// 不可变，通过工厂方法创建。
/// </summary>
public sealed record PriceChangeRecord
{
    /// <summary>SKU 标识。</summary>
    public string SkuId { get; private set; } = string.Empty;

    /// <summary>变更前价格。</summary>
    public decimal OldPrice { get; private set; }

    /// <summary>变更后价格。</summary>
    public decimal NewPrice { get; private set; }

    /// <summary>变更时间（UTC）。</summary>
    public DateTime ChangedAt { get; private set; }

    /// <summary>变更人标识。</summary>
    public string ChangedBy { get; private set; } = string.Empty;

    private PriceChangeRecord() { }

    private PriceChangeRecord(string skuId, decimal oldPrice, decimal newPrice, DateTime changedAt, string changedBy)
    {
        SkuId = skuId;
        OldPrice = oldPrice;
        NewPrice = newPrice;
        ChangedAt = changedAt;
        ChangedBy = changedBy;
    }

    /// <summary>
    /// 创建价格变更记录。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="oldPrice">变更前价格。</param>
    /// <param name="newPrice">变更后价格。</param>
    /// <param name="changedBy">变更人标识。</param>
    public static PriceChangeRecord Create(string skuId, decimal oldPrice, decimal newPrice, string changedBy)
    {
        if (string.IsNullOrWhiteSpace(skuId))
        {
            throw new ArgumentException("SKU 标识不可为空", nameof(skuId));
        }

        if (string.IsNullOrWhiteSpace(changedBy))
        {
            throw new ArgumentException("变更人标识不可为空", nameof(changedBy));
        }

        if (newPrice <= 0)
        {
            throw new ArgumentException("新价格须大于 0", nameof(newPrice));
        }

        return new PriceChangeRecord(skuId.Trim(), oldPrice, newPrice, DateTime.UtcNow, changedBy.Trim());
    }
}