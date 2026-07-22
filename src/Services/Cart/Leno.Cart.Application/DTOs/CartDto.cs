namespace Leno.Cart.Application.DTOs;

/// <summary>
/// 购物车项 DTO，含实时价格与可售状态。
/// </summary>
public sealed class CartItemDto
{
    public Guid Id { get; init; }

    public Guid SkuId { get; init; }

    public Guid SellerId { get; init; }

    public int Quantity { get; init; }

    public bool IsSelected { get; init; }

    public Guid SourceCartItemId { get; init; }

    /// <summary>实时单价（由防腐层查询商品域）。价格加载失败时为 0，需结合 <see cref="PriceUnavailable"/> 判断。</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>小计金额。价格加载失败时为 0，前端不应据此展示可结算金额。</summary>
    public decimal Subtotal => UnitPrice * Quantity;

    public string Currency { get; init; } = "CNY";

    /// <summary>商品标题（展示用）。价格加载失败时为 "[价格加载失败]"。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>主图 URL（展示用）。</summary>
    public string MainImageUrl { get; init; } = string.Empty;

    /// <summary>是否可售（在售且有库存）。</summary>
    public bool Available { get; init; }

    /// <summary>
    /// 价格是否加载失败（价格服务不可用或未命中 SKU）。
    /// true 时 <see cref="UnitPrice"/> 不应作为结算依据，前端须禁止该商品结算并提示用户。
    /// </summary>
    public bool PriceUnavailable { get; init; }
}

/// <summary>
/// 购物车 DTO。
/// </summary>
public sealed class CartDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public IReadOnlyList<CartItemDto> Items { get; init; } = Array.Empty<CartItemDto>();

    /// <summary>
    /// 选中项总金额。仅在单币种时填充；混币种时为 0，前端按 <see cref="SubtotalsByCurrency"/> 展示。
    /// </summary>
    public decimal SelectedTotalAmount { get; init; }

    public string Currency { get; init; } = "CNY";

    /// <summary>购物车项总数。</summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 按币种分组的选中项小计（仅含价格可用项）。
    /// 单币种时仅 1 个条目且与 <see cref="SelectedTotalAmount"/> 一致；
    /// 混币种时多个条目，前端按此字段分别展示各币种金额。
    /// </summary>
    public IReadOnlyDictionary<string, decimal> SubtotalsByCurrency { get; init; } = new Dictionary<string, decimal>();
}
