namespace Leno.SellerShop.Application;

/// <summary>
/// 低库存商品 DTO（卖家域视角），由 ACL 从商品域 LowStockSkuDto 映射。
/// </summary>
public sealed class LowStockItemDto
{
    public Guid SkuId { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string SkuName { get; init; } = string.Empty;

    public int Stock { get; init; }

    public int Threshold { get; init; }
}
