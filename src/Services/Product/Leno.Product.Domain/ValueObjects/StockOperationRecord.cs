namespace Leno.Product.Domain.ValueObjects;

/// <summary>
/// 库存操作记录值对象，记录每次库存变更的操作人、时间、变动量与操作后库存。
/// 不可变，通过工厂方法创建。
/// </summary>
public sealed record StockOperationRecord
{
    /// <summary>SKU 标识。</summary>
    public string SkuId { get; private set; } = string.Empty;

    /// <summary>操作人标识。</summary>
    public string Operator { get; private set; } = string.Empty;

    /// <summary>库存变动量（正数为补货，负数为扣减）。</summary>
    public int Delta { get; private set; }

    /// <summary>操作后库存量。</summary>
    public int NewStock { get; private set; }

    /// <summary>操作时间（UTC）。</summary>
    public DateTime OperatedAt { get; private set; }

    private StockOperationRecord() { }

    private StockOperationRecord(string skuId, string @operator, int delta, int newStock, DateTime operatedAt)
    {
        SkuId = skuId;
        Operator = @operator;
        Delta = delta;
        NewStock = newStock;
        OperatedAt = operatedAt;
    }

    /// <summary>
    /// 创建库存操作记录。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="operator">操作人标识。</param>
    /// <param name="delta">库存变动量。</param>
    /// <param name="newStock">操作后库存量。</param>
    public static StockOperationRecord Create(string skuId, string @operator, int delta, int newStock)
    {
        if (string.IsNullOrWhiteSpace(skuId))
        {
            throw new ArgumentException("SKU 标识不可为空", nameof(skuId));
        }

        if (string.IsNullOrWhiteSpace(@operator))
        {
            throw new ArgumentException("操作人标识不可为空", nameof(@operator));
        }

        return new StockOperationRecord(skuId.Trim(), @operator.Trim(), delta, newStock, DateTime.UtcNow);
    }
}