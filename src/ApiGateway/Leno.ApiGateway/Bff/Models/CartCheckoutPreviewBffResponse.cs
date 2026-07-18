using System.Text.Json;

namespace Leno.ApiGateway.Bff.Models;

/// <summary>
/// 购物车结算预览聚合响应：购物车预览 + 优惠明细 + 积分抵扣。
/// </summary>
public sealed class CartCheckoutPreviewBffResponse
{
    /// <summary>购物车预览（来自 Cart BC <c>/api/cart/preview</c>）。</summary>
    public JsonElement? CartPreview { get; init; }

    /// <summary>优惠明细（来自 Promotion BC <c>/internal/v1/promotions/calculate</c>）。</summary>
    public JsonElement? Promotion { get; init; }

    /// <summary>积分抵扣试算（来自 PointsMembership BC <c>/internal/v1/points/trial-offset</c>）。</summary>
    public JsonElement? PointsTrialOffset { get; init; }
}
