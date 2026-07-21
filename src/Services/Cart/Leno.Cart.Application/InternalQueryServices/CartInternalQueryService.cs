using Leno.Cart.Application.Services;

namespace Leno.Cart.Application.InternalQueryServices;

/// <summary>
/// 购物车域跨 BC 内部查询服务实现（M4 双轨方案）。
/// 委托 <see cref="ICartAppService"/> 的既有查询方法，映射为跨 BC DTO。
/// </summary>
/// <remarks>
/// P1-10：金额转分采用 <see cref="MidpointRounding.AwayFromZero"/> 四舍五入，避免向零截断丢失分位。
/// P1-11：购物车不存在时返回 null（不创建），使 gRPC NotFound 分支可达。
/// </remarks>
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
        // P1-11：购物车不存在时 FindCartAsync 返回 null（不创建新空购物车），gRPC NotFound 分支可达
        var cart = await _cartAppService.FindCartAsync(userId, ct);
        if (cart is null || cart.Items.Count == 0)
        {
            return null;
        }

        return new CartSnapshotDto
        {
            CartId = cart.Id,
            Items = cart.Items.Select(i => new CartItemSnapshotDto
            {
                SkuId = i.SkuId,
                Quantity = i.Quantity,
                UnitPriceCents = ToCents(i.UnitPrice)
            }).ToList(),
            TotalCents = ToCents(cart.SelectedTotalAmount)
        };
    }

    /// <inheritdoc />
    public async Task<CheckoutPreviewSnapshotDto?> GetCheckoutPreviewAsync(Guid userId, CancellationToken ct = default)
    {
        CheckoutPreviewSnapshotDto? result;
        try
        {
            var preview = await _cartAppService.PreviewCheckoutAsync(userId, ct);
            if (preview is null || preview.Groups.Count == 0)
            {
                return null;
            }

            result = new CheckoutPreviewSnapshotDto
            {
                // CheckoutPreviewDto（Application.DTOs）当前未提供折扣/运费字段，
                // 跨 BC 查询以"小计 = 各卖家分组小计之和、折扣 = 0、运费 = 0、合计 = TotalAmount"近似映射
                SubtotalCents = ToCents(preview.Groups.Sum(g => g.SubtotalAmount)),
                DiscountCents = 0,
                ShippingCents = 0,
                TotalCents = ToCents(preview.TotalAmount)
            };
        }
        catch (Leno.Cart.Domain.Exceptions.CartDomainException)
        {
            // 用户购物车不存在或无选中项等业务异常视为"无可结算预览"，返回 null
            return null;
        }

        return result;
    }

    /// <summary>
    /// 将金额（元，decimal）转换为分（long），使用四舍五入（MidpointRounding.AwayFromZero）
    /// 避免向零截断丢失分位（例如 19.999m 应转为 2000 而非 1999）。
    /// </summary>
    private static long ToCents(decimal value)
        => (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero);
}
