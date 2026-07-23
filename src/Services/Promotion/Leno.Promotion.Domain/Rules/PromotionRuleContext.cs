namespace Leno.Promotion.Domain.Rules;

/// <summary>
/// 规则评估上下文，承载一次促销试算的全部输入。
/// 不可变记录（record），便于规则编排时通过 <c>with</c> 表达式派生新上下文（如扣减剩余 SubTotal）。
/// </summary>
public sealed record PromotionRuleContext
{
    /// <summary>买家标识。</summary>
    public required long UserId { get; init; }

    /// <summary>卖家标识（0 表示多卖家聚合订单）。</summary>
    public required long SellerId { get; init; }

    /// <summary>购物车行列表（不可空，至少 1 行；空订单不应进入规则引擎）。</summary>
    public required IReadOnlyList<CartItemContext> Items { get; init; }

    /// <summary>
    /// 订单小计金额（所有 Items 的 Subtotal 之和）。
    /// 规则引擎在评估期间会基于此值计算剩余可抵扣金额，<see cref="IRuleEngine"/> 在编排时通过 <c>with</c> 派生新上下文。
    /// </summary>
    public required decimal SubTotal { get; init; }

    /// <summary>用户优惠券码（若用户在试算时指定，<c>null</c> 表示不使用优惠券）。</summary>
    public string? CouponCode { get; init; }

    /// <summary>秒杀活动标识（若订单命中秒杀活动，<c>null</c> 表示非秒杀订单）。</summary>
    public string? SeckillActivityId { get; init; }

    /// <summary>
    /// 扩展属性字典（如会员等级、渠道、租户等），规则可读取自定义上下文。
    /// 不可空，无扩展属性时为空字典。
    /// </summary>
    public required IReadOnlyDictionary<string, string> Attributes { get; init; }

    /// <summary>评估时间（UTC），默认 <see cref="DateTime.UtcNow"/>，便于规则判断活动有效期。</summary>
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 购物车行上下文，规则评估时引用的 SKU 维度信息。
/// </summary>
public sealed record CartItemContext
{
    /// <summary>SKU 标识。</summary>
    public required Guid SkuId { get; init; }

    /// <summary>购买数量。</summary>
    public required int Quantity { get; init; }

    /// <summary>单价（已含快照价格或秒杀价）。</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>分类编码（用于类目限定规则，<c>null</c> 表示未指定）。</summary>
    public string? CategoryCode { get; init; }

    /// <summary>本行小计 = <see cref="UnitPrice"/> * <see cref="Quantity"/>。</summary>
    public decimal Subtotal => UnitPrice * Quantity;
}
