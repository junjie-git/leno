namespace Leno.Order.Domain.Services;

/// <summary>
/// 订单定价预览领域服务接口（P1-T18）。
/// 复用聚合根 <see cref="Aggregates.Order"/> 的金额不变量与积分上限裁剪逻辑，
/// 供应用层 <c>PreviewAsync</c> 调用以避免重复实现 <c>TotalAmount = ItemsAmount - Discount - Points + Freight</c> 公式。
/// </summary>
public interface IOrderPricingPreviewService
{
    /// <summary>
    /// 预览订单金额：按 SKU 小计比例分摊优惠，校验积分抵现上限（ItemsAmount - Discount 与 MaxPointsOffsetAmount），
    /// 复用 <see cref="Aggregates.Order.RecalculateTotal"/> 等价的金额公式返回最终金额。
    /// </summary>
    /// <param name="items">预览明细列表（含 SKU、单价、数量与小计）。</param>
    /// <param name="totalDiscount">优惠总金额，须 ≥ 0 且 ≤ 商品总额。</param>
    /// <param name="pointsOffsetRaw">积分抵现原始金额，由调用方按用户输入积分/100 转换（未裁剪）。</param>
    /// <param name="freightAmount">运费金额，由调用方按卖家与区域计算后求和传入。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>预览结果，含各金额字段与明细。</returns>
    Task<OrderPreviewResult> PreviewAsync(
        IReadOnlyList<OrderPreviewItem> items,
        decimal totalDiscount,
        decimal pointsOffsetRaw,
        decimal freightAmount,
        CancellationToken ct = default);
}

/// <summary>
/// 预览明细项（领域层值对象），表达单 SKU 的预览输入。
/// </summary>
public sealed record OrderPreviewItem
{
    /// <summary>SKU 标识。</summary>
    public required Guid SkuId { get; init; }

    /// <summary>商品名称（仅用于回显，不参与金额计算）。</summary>
    public required string ProductName { get; init; }

    /// <summary>成交单价，须 ≥ 0。</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>购买数量，须 &gt; 0。</summary>
    public required int Quantity { get; init; }

    /// <summary>小计金额 = UnitPrice × Quantity，须 ≥ 0。</summary>
    public required decimal Subtotal { get; init; }
}

/// <summary>
/// 预览结果（领域层值对象），含最终金额与明细。
/// 金额公式：<see cref="TotalAmount"/> = <see cref="ItemsAmount"/> - <see cref="DiscountAmount"/> - <see cref="PointsOffsetAmount"/> + <see cref="FreightAmount"/>。
/// </summary>
public sealed record OrderPreviewResult
{
    /// <summary>商品总金额（明细小计之和）。</summary>
    public required decimal ItemsAmount { get; init; }

    /// <summary>优惠总金额。</summary>
    public required decimal DiscountAmount { get; init; }

    /// <summary>积分抵现金额（已裁剪至 ItemsAmount - Discount 与 MaxPointsOffsetAmount）。</summary>
    public required decimal PointsOffsetAmount { get; init; }

    /// <summary>运费金额。</summary>
    public required decimal FreightAmount { get; init; }

    /// <summary>订单总金额（实付）。</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>明细列表（含分摊后的优惠金额）。</summary>
    public required IReadOnlyList<OrderPreviewItemDetail> Items { get; init; }
}

/// <summary>
/// 预览明细详情（领域层值对象），含分摊后的优惠金额。
/// </summary>
public sealed record OrderPreviewItemDetail
{
    /// <summary>SKU 标识。</summary>
    public required Guid SkuId { get; init; }

    /// <summary>商品名称。</summary>
    public required string ProductName { get; init; }

    /// <summary>成交单价。</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>购买数量。</summary>
    public required int Quantity { get; init; }

    /// <summary>小计金额。</summary>
    public required decimal Subtotal { get; init; }

    /// <summary>分摊的优惠金额。</summary>
    public required decimal DiscountAllocation { get; init; }
}
