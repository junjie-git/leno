namespace Leno.Cart.Application.DTOs;

/// <summary>
/// 商品域 SKU 快照 DTO，用于购物车展示刷新。
/// 经 <see cref="Leno.Cart.Application.Abstractions.IProductSnapshotAntiCorruption"/> 从商品域 internal API 获取。
/// </summary>
public sealed class SkuSnapshotDto
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; set; }

    /// <summary>商品标题（用于购物车展示）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>主图 URL（用于购物车展示）。</summary>
    public string? MainImageUrl { get; set; }

    /// <summary>SKU 单价。</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>是否在售。</summary>
    public bool IsOnSale { get; set; }
}
