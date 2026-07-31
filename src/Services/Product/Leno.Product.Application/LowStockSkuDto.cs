namespace Leno.Product.Application;

/// <summary>
/// 低库存 SKU 查询结果 DTO（商品域内部，供跨 BC ACL 调用）。
/// 数据来自 SPU 聚合内 SKU 实体的 StockQty 字段。
/// </summary>
public sealed class LowStockSkuDto
{
    public Guid SkuId { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string SkuName { get; init; } = string.Empty;

    public int Stock { get; init; }

    public int Threshold { get; init; }

    public Guid ShopId { get; init; }
}
