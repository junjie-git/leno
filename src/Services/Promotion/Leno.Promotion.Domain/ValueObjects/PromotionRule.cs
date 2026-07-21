using System.Text.Json.Serialization;

namespace Leno.Promotion.Domain.ValueObjects;

/// <summary>
/// 满减规则值对象，表达“满 <see cref="ThresholdAmount"/> 减 <see cref="DiscountAmount"/>”。
/// 不可变，按值相等，可有多档规则（阶梯满减）。
/// </summary>
public sealed record PromotionRule
{
    /// <summary>门槛金额（订单金额须 ≥ 此值方可命中规则）。</summary>
    public decimal ThresholdAmount { get; init; }

    /// <summary>减免金额（须 &gt; 0 且 ≤ 门槛金额）。</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>
    /// 唯一构造，由 <see cref="JsonConstructorAttribute"/> 标注，
    /// 让 System.Text.Json 与 EF Core 10 JSON 列反序列化均走此构造路径，
    /// 保证 init setter 不可绕过、不变量校验统一生效。
    /// </summary>
    [JsonConstructor]
    public PromotionRule(decimal thresholdAmount, decimal discountAmount)
    {
        if (thresholdAmount < 0)
        {
            throw new ArgumentException("门槛金额不可为负", nameof(thresholdAmount));
        }

        if (discountAmount <= 0)
        {
            throw new ArgumentException("减免金额须大于 0", nameof(discountAmount));
        }

        if (discountAmount > thresholdAmount)
        {
            throw new ArgumentException("减免金额不可超过门槛金额", nameof(discountAmount));
        }

        ThresholdAmount = thresholdAmount;
        DiscountAmount = discountAmount;
    }
}
