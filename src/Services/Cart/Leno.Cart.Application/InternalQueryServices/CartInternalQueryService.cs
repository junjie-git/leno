using Leno.Cart.Application.Services;

namespace Leno.Cart.Application.InternalQueryServices;

/// <summary>
/// 购物车域跨 BC 内部查询服务实现（M4 双轨方案）。
/// 委托 <see cref="ICartAppService"/> 的既有查询方法，映射为跨 BC DTO。
/// </summary>
public sealed class CartInternalQueryService : ICartInternalQueryService
{
    private readonly ICartAppService _cartAppService;

    public CartInternalQueryService(ICartAppService cartAppService)
    {
        _cartAppService = cartAppService ?? throw new ArgumentNullException(nameof(cartAppService));
    }

    /// <inheritdoc />
    public async Task<CartSnapshotDto?> GetCartSnapshotAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await _cartAppService.GetCartAsync(userId, ct);
        if (cart is null) return null;

        return new CartSnapshotDto
        {
            CartId = cart.Id,
            Items = cart.Items.Select(i => new CartItemSnapshotDto
            {
                SkuId = i.SkuId,
                Quantity = i.Quantity,
                UnitPriceCents = (long)(i.UnitPrice * 100)
            }).ToList(),
            TotalCents = (long)(cart.SelectedTotalAmount * 100)
        };
    }

    /// <inheritdoc />
    public async Task<CheckoutPreviewSnapshotDto?> GetCheckoutPreviewAsync(Guid userId, CancellationToken ct = default)
    {
        var preview = await _cartAppService.PreviewCheckoutAsync(userId, ct);
        if (preview is null) return null;

        return new CheckoutPreviewSnapshotDto
        {
            // CheckoutPreviewDto（Application.DTOs）当前未提供折扣/运费字段，
            // 跨 BC 查询以"小计 = 各卖家分组小计之和、折扣 = 0、运费 = 0、合计 = TotalAmount"近似映射
            SubtotalCents = (long)(preview.Groups.Sum(g => g.SubtotalAmount) * 100),
            DiscountCents = 0,
            ShippingCents = 0,
            TotalCents = (long)(preview.TotalAmount * 100)
        };
    }
}
