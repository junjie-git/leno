using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Leno.SharedKernel.ValueObjects;

namespace Leno.Product.Domain.Aggregates;

/// <summary>
/// SKU 实体，作为 SPU 聚合内的实体而非独立聚合根。
/// 规格组合唯一性、价格 > 0、库存 ≥ 0 等不变量由 SPU 聚合根统一保证。
/// 外部上下文不可直接引用，仅通过 SPU 聚合根访问。
/// </summary>
public sealed class SKU : Entity
{
    /// <summary>所属 SPU 标识。</summary>
    public Guid SpuId { get; private set; }

    /// <summary>商家自定义编码，同 SPU 下唯一。</summary>
    public string SkuCode { get; private set; } = string.Empty;

    /// <summary>销售价格。</summary>
    public Money Price { get; private set; } = null!;

    /// <summary>可售库存基线（卖家补货/盘点修正的权威值，高频预占在订单域 Redis 完成）。</summary>
    public int StockQty { get; private set; }

    /// <summary>规格属性集合。</summary>
    public SkuSpec SpecAttributes { get; private set; } = null!;

    /// <summary>SKU 状态。</summary>
    public SkuStatus Status { get; private set; }

    /// <summary>SKU 专属图，可空。</summary>
    public string? ImageUrl { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private SKU() { }

    private SKU(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建 SKU 实体并附加到 SPU 聚合。
    /// </summary>
    /// <param name="skuId">SKU 标识，由应用层生成。</param>
    /// <param name="spuId">所属 SPU 标识。</param>
    /// <param name="skuCode">商家自定义编码。</param>
    /// <param name="price">销售价格，须 > 0。</param>
    /// <param name="stockQty">库存基线，须 ≥ 0。</param>
    /// <param name="specAttributes">规格属性集合，至少 1 项。</param>
    /// <param name="imageUrl">SKU 专属图，可空。</param>
    public static SKU Create(
        Guid skuId,
        Guid spuId,
        string skuCode,
        Money price,
        int stockQty,
        SkuSpec specAttributes,
        string? imageUrl = null)
    {
        if (skuId == Guid.Empty)
        {
            throw new ProductDomainException("SKU 标识不可为空", "SKU_ID_EMPTY");
        }

        if (spuId == Guid.Empty)
        {
            throw new ProductDomainException("SPU 标识不可为空", "SKU_SPU_EMPTY");
        }

        ValidateSkuCode(skuCode);
        ValidatePrice(price);
        ValidateStock(stockQty);
        ArgumentNullException.ThrowIfNull(specAttributes);

        return new SKU(skuId)
        {
            SpuId = spuId,
            SkuCode = skuCode.Trim(),
            Price = price,
            StockQty = stockQty,
            SpecAttributes = specAttributes,
            Status = SkuStatus.Active,
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim()
        };
    }

    /// <summary>
    /// 调整 SKU 价格，校验价格 > 0。
    /// </summary>
    public void UpdatePrice(Money newPrice)
    {
        ValidatePrice(newPrice);
        Price = newPrice;
    }

    /// <summary>
    /// 调整 SKU 库存基线（卖家补货/盘点修正），校验结果 ≥ 0。
    /// </summary>
    /// <param name="newStockQty">调整后的库存量。</param>
    public void UpdateStock(int newStockQty)
    {
        ValidateStock(newStockQty);
        StockQty = newStockQty;
    }

    /// <summary>
    /// 启用 SKU。
    /// </summary>
    public void Activate()
    {
        Status = SkuStatus.Active;
    }

    /// <summary>
    /// 停用 SKU。
    /// </summary>
    public void Deactivate()
    {
        Status = SkuStatus.Inactive;
    }

    private static void ValidateSkuCode(string skuCode)
    {
        if (string.IsNullOrWhiteSpace(skuCode))
        {
            throw new ProductDomainException("SKU 编码不可为空", "SKU_CODE_EMPTY");
        }

        if (skuCode.Trim().Length > 64)
        {
            throw new ProductDomainException("SKU 编码长度不可超过 64 字符", "SKU_CODE_LENGTH");
        }
    }

    private static void ValidatePrice(Money price)
    {
        ArgumentNullException.ThrowIfNull(price);
        // 修复审计 #11：委托 Money.RequirePositive() 校验金额 > 0，保持 ProductDomainException 语义。
        try
        {
            price.RequirePositive();
        }
        catch (ArgumentException)
        {
            throw new ProductDomainException("SKU 价格须大于 0", "SKU_PRICE_INVALID");
        }
    }

    private static void ValidateStock(int stockQty)
    {
        if (stockQty < 0)
        {
            throw new ProductDomainException("SKU 库存不可为负", "SKU_STOCK_NEGATIVE");
        }
    }
}
