using Leno.Order.Domain.Exceptions;

namespace Leno.Order.Domain.ValueObjects;

/// <summary>
/// 商品快照值对象，下单时固化商品名称、SKU 名称、主图与卖家，避免商品信息变更影响历史订单。
/// 采用 sealed class（非 record）以便 EF Core 作为 owned type 映射。
/// </summary>
public sealed class ProductSnapshot
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>SPU 标识。</summary>
    public Guid SpuId { get; private set; }

    /// <summary>商品名称。</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>SKU 名称（规格描述）。</summary>
    public string SkuName { get; private set; } = string.Empty;

    /// <summary>主图地址，可为空。</summary>
    public string? MainImage { get; private set; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private ProductSnapshot() { }

    private ProductSnapshot(Guid skuId, Guid spuId, string productName, string skuName, string? mainImage, Guid sellerId)
    {
        SkuId = skuId;
        SpuId = spuId;
        ProductName = productName;
        SkuName = skuName;
        MainImage = mainImage;
        SellerId = sellerId;
    }

    /// <summary>
    /// 工厂方法，校验标识非空与名称非空后创建商品快照。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="spuId">SPU 标识。</param>
    /// <param name="productName">商品名称。</param>
    /// <param name="skuName">SKU 名称。</param>
    /// <param name="mainImage">主图地址，可为空。</param>
    /// <param name="sellerId">卖家标识。</param>
    public static ProductSnapshot Create(
        Guid skuId,
        Guid spuId,
        string productName,
        string skuName,
        string? mainImage,
        Guid sellerId)
    {
        if (skuId == Guid.Empty)
        {
            throw new OrderDomainException("SkuId 不可为空", "ORDER_SNAPSHOT_SKU_EMPTY");
        }

        if (spuId == Guid.Empty)
        {
            throw new OrderDomainException("SpuId 不可为空", "ORDER_SNAPSHOT_SPU_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new OrderDomainException("商品名称不可为空", "ORDER_SNAPSHOT_PRODUCT_NAME_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(skuName))
        {
            throw new OrderDomainException("SKU 名称不可为空", "ORDER_SNAPSHOT_SKU_NAME_EMPTY");
        }

        if (sellerId == Guid.Empty)
        {
            throw new OrderDomainException("SellerId 不可为空", "ORDER_SNAPSHOT_SELLER_EMPTY");
        }

        return new ProductSnapshot(skuId, spuId, productName, skuName, mainImage, sellerId);
    }
}
