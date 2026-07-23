namespace Leno.Promotion.Domain.Rules;

/// <summary>
/// 规则优先级特性，标注于 <see cref="IPromotionRule"/> 实现类，
/// 由 <see cref="IRuleEngine"/> 编排时按 <see cref="Priority"/> 升序评估规则（数字越小越先评估）。
/// 当规则实现同时实现 <see cref="IPromotionRule.Priority"/> 属性时，运行时优先取属性值；
/// 此特性用于在静态声明层表达"默认优先级"，便于配置扫描与可视化展示。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RulePriorityAttribute : Attribute
{
    /// <summary>
    /// 默认优先级。数字越小越先评估。
    /// 约定：满减 = 100，优惠券 = 200，秒杀折扣 = 50（互斥场景优先评估）。
    /// </summary>
    public int Priority { get; }

    /// <summary>规则中文名，便于诊断与可视化展示。</summary>
    public string DisplayName { get; }

    public RulePriorityAttribute(int priority, string displayName)
    {
        if (priority < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "优先级不可为负");
        }

        ArgumentNullException.ThrowIfNull(displayName);
        Priority = priority;
        DisplayName = displayName;
    }
}
