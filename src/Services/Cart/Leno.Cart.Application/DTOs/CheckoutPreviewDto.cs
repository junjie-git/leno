namespace Leno.Cart.Application.DTOs;

/// <summary>
/// 结算预览按卖家分组的购物车项快照。
/// </summary>
public sealed class CheckoutGroupDto
{
    /// <summary>卖家（店铺）标识。</summary>
    public Guid SellerId { get; init; }

    /// <summary>该卖家的购物车项列表。</summary>
    public IReadOnlyList<CartItemDto> Items { get; init; } = Array.Empty<CartItemDto>();

    /// <summary>该卖家分组小计金额。</summary>
    public decimal SubtotalAmount { get; init; }

    public string Currency { get; init; } = "CNY";
}

/// <summary>
/// 结算预览结果，按卖家分组返回选中项。
/// </summary>
public sealed class CheckoutPreviewDto
{
    public IReadOnlyList<CheckoutGroupDto> Groups { get; init; } = Array.Empty<CheckoutGroupDto>();

    /// <summary>
    /// 合计金额。仅在单币种时填充；混币种时为 0，调用方按 <see cref="SubtotalsByCurrency"/> 展示。
    /// </summary>
    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = "CNY";

    /// <summary>选中项总数。</summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 按币种分组的选中项小计（仅含价格可用项）。
    /// 单币种时仅 1 个条目且与 <see cref="TotalAmount"/> 一致；
    /// 混币种时多个条目，前端按此字段分别展示各币种金额。
    /// </summary>
    public IReadOnlyDictionary<string, decimal> SubtotalsByCurrency { get; init; } = new Dictionary<string, decimal>();
}
