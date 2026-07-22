namespace Leno.SharedContracts.Integration.Dto;

/// <summary>
/// 商品快照共享 DTO（D2.3 ACL 模式去重）。
/// 各 BC 的 ProductSnapshot ACL 防腐层统一返回此类型，消除 Cart/Order/Promotion 3 BC 重复定义。
/// 字段为各 BC 需求的超集，购物车域仅用 SkuId/Title/UnitPrice/IsOnSale，订单域快照用全部字段。
/// </summary>
public sealed class ProductSnapshotDto
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>商品名称（SPU 级别）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>SKU 名称（规格描述）。</summary>
    public string SkuName { get; init; } = string.Empty;

    /// <summary>SKU 单价。</summary>
    public decimal Price { get; init; }

    /// <summary>库存数量（可选，仅商品域查询时填充）。</summary>
    public int Stock { get; init; }

    /// <summary>主图 URL。</summary>
    public string? ImageUrl { get; init; }

    /// <summary>是否在售。</summary>
    public bool IsOnSale { get; init; }

    /// <summary>卖家（店铺）标识。</summary>
    public Guid SellerId { get; init; }
}
