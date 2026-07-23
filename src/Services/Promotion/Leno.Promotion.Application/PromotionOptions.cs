namespace Leno.Promotion.Application;

/// <summary>
/// 促销域功能开关与可配置选项，绑定自配置节 <c>Promotion</c>。
/// 用于 A/B 测试灰度切换新旧促销试算路径。
/// </summary>
public sealed class PromotionOptions
{
    /// <summary>配置节名称，对应 appsettings.json 中的 <c>Promotion</c> 节。</summary>
    public const string SectionName = "Promotion";

    /// <summary>
    /// 是否启用规则引擎路径进行促销试算。
    /// <list type="bullet">
    /// <item><c>true</c>：<see cref="Services.PromotionCalculateAppService"/> 调用 <see cref="Domain.Rules.IRuleEngine"/> 编排所有 <see cref="Domain.Rules.IPromotionRule"/>；</item>
    /// <item><c>false</c>（默认）：走旧硬编码试算路径（满减 + 优惠券直接相加），保证向后兼容。</item>
    /// </list>
    /// 灰度发布时先在小流量开启，验证规则引擎结果与旧路径一致后全量切换。
    /// </summary>
    public bool UseRuleEngine { get; set; }
}
