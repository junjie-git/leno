namespace Leno.Cart.Application.DTOs;

/// <summary>
/// 添加购物车项 DTO。
/// </summary>
public sealed class AddCartItemDto
{
    public Guid SkuId { get; init; }

    public int Quantity { get; init; } = 1;

    public Guid SellerId { get; init; }
}

/// <summary>
/// 更新购物车项数量 DTO。
/// </summary>
public sealed class UpdateCartItemQuantityDto
{
    public int Quantity { get; init; } = 1;
}

/// <summary>
/// 批量选中/取消选中 DTO。
/// </summary>
public sealed class SelectCartItemsDto
{
    /// <summary>待操作的 SKU 标识列表。</summary>
    public IReadOnlyList<Guid> SkuIds { get; init; } = Array.Empty<Guid>();

    /// <summary>true=选中，false=取消选中。</summary>
    public bool Selected { get; init; } = true;
}
