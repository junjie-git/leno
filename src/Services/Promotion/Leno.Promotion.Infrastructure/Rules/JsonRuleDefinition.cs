using System.Text.Json;
using System.Text.Json.Serialization;

namespace Leno.Promotion.Infrastructure.Rules;

/// <summary>
/// 规则定义 JSON 绑定模型，由 <see cref="JsonRuleLoader"/> 从 <see cref="Leno.Promotion.Domain.Aggregates.PromotionRuleDefinition.DefinitionJson"/>
/// 反序列化得到。规则实现按 <see cref="RuleType"/> 读取对应字段（满减读 <see cref="Thresholds"/>、优惠券读 <see cref="CouponFilter"/>、秒杀读 <see cref="SeckillFilter"/>）。
/// </summary>
public sealed class JsonRuleDefinition
{
    /// <summary>规则类型标识（与 <see cref="Leno.Promotion.Domain.Rules.IPromotionRule.RuleType"/> 对齐）。</summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>优先级，从 <see cref="Leno.Promotion.Domain.Aggregates.PromotionRuleDefinition.Priority"/> 同步。</summary>
    public int Priority { get; set; }

    /// <summary>叠加策略，从 <see cref="Leno.Promotion.Domain.Aggregates.PromotionRuleDefinition.Stacking"/> 同步。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Leno.Promotion.Domain.Rules.StackingPolicy Stacking { get; set; }

    /// <summary>满减档位集合（仅 RuleType=FullReduction 使用），按 <see cref="FullReductionThreshold.MinAmount"/> 升序。</summary>
    public List<FullReductionThreshold> Thresholds { get; set; } = new();

    /// <summary>优惠券过滤配置（仅 RuleType=Coupon 使用），可指定适用券模板 Id 集合与卖家/类目限定。</summary>
    public CouponFilterDefinition? CouponFilter { get; set; }

    /// <summary>秒杀过滤配置（仅 RuleType=SeckillDiscount 使用），可指定秒杀活动 Id 与卖家限定。</summary>
    public SeckillFilterDefinition? SeckillFilter { get; set; }

    /// <summary>适用卖家 Id 集合（空集合表示不限卖家），所有规则类型共用。</summary>
    public List<long> ApplicableSellerIds { get; set; } = new();

    /// <summary>适用类目编码集合（空集合表示不限类目），所有规则类型共用。</summary>
    public List<string> ApplicableCategoryCodes { get; set; } = new();

    /// <summary>
    /// 从 JSON 字符串反序列化为 <see cref="JsonRuleDefinition"/>，使用 snake_case 命名策略与枚举字符串绑定。
    /// 反序列化失败（空串或无效 JSON）时返回 <c>null</c>，由调用方决定是否跳过该定义。
    /// </summary>
    public static JsonRuleDefinition? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonRuleDefinition>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>序列化为 JSON 字符串。</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>JSON 序列化选项：snake_case 命名策略 + 枚举字符串 + 大小写无关匹配。</summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// 满减档位：满 <see cref="MinAmount"/> 减 <see cref="DiscountAmount"/>。
/// 与 <see cref="Leno.Promotion.Domain.ValueObjects.PromotionRule"/> 含义一致，但作为 JSON 绑定模型独立存在以便配置文件直接编辑。
/// </summary>
public sealed class FullReductionThreshold
{
    /// <summary>门槛金额，订单金额须 ≥ 此值方可命中。</summary>
    public decimal MinAmount { get; set; }

    /// <summary>减免金额，须 &gt; 0 且 ≤ <see cref="MinAmount"/>。</summary>
    public decimal DiscountAmount { get; set; }
}

/// <summary>
/// 优惠券过滤配置：可选指定适用券模板 Id 集合（仅这些券可参与折扣试算）。
/// 空集合表示用户所有 Unused 且未过期券均可参与。
/// </summary>
public sealed class CouponFilterDefinition
{
    /// <summary>适用券模板 Id 集合（空表示不限模板）。</summary>
    public List<Guid> ApplicableCouponIds { get; set; } = new();

    /// <summary>是否仅使用用户传入的 <c>CouponCode</c>（true 表示按指定券码抵扣，false 表示自动选最优）。</summary>
    public bool UseCouponCodeOnly { get; set; }
}

/// <summary>
/// 秒杀过滤配置：可选指定秒杀活动 Id（若 <see cref="Leno.Promotion.Domain.Rules.PromotionRuleContext.SeckillActivityId"/> 为空时使用）。
/// </summary>
public sealed class SeckillFilterDefinition
{
    /// <summary>默认秒杀活动 Id（context 未指定时使用）。</summary>
    public Guid? DefaultActivityId { get; set; }

    /// <summary>秒杀折扣率（0-100），用于按 (OriginalPrice - SeckillPrice) 计算时折算比例（如 100 表示全折扣，50 表示半折扣）。默认 100。</summary>
    public decimal DiscountRatio { get; set; } = 100m;
}
