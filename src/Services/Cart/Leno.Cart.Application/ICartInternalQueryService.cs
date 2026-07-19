namespace Leno.Cart.Application;

/// <summary>
/// 购物车域跨 BC 内部查询服务（M4 双轨方案）。
/// 仅暴露跨 BC 查询所需的方法子集（只读），供 CartGrpcService 复用。
/// </summary>
public interface ICartInternalQueryService
{
    /// <summary>
    /// 查询用户购物车快照（含购物车项）。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>购物车快照；购物车不存在返回 null。</returns>
    Task<CartSnapshotDto?> GetCartSnapshotAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 查询用户结账预览（含金额汇总）。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>结账预览；购物车不存在返回 null。</returns>
    Task<CheckoutPreviewSnapshotDto?> GetCheckoutPreviewAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>购物车快照 DTO（跨 BC 查询用）。</summary>
public sealed class CartSnapshotDto
{
    public Guid CartId { get; init; }
    public IReadOnlyList<CartItemSnapshotDto> Items { get; init; } = Array.Empty<CartItemSnapshotDto>();
    public long TotalCents { get; init; }
}

public sealed class CartItemSnapshotDto
{
    public Guid SkuId { get; init; }
    public int Quantity { get; init; }
    public long UnitPriceCents { get; init; }
}

public sealed class CheckoutPreviewSnapshotDto
{
    public long SubtotalCents { get; init; }
    public long DiscountCents { get; init; }
    public long ShippingCents { get; init; }
    public long TotalCents { get; init; }
}
