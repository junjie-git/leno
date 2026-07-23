using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Events;

/// <summary>
/// 促销规则定义变更领域事件，由 <see cref="Aggregates.PromotionRuleDefinition"/> 聚合在
/// 创建后变更（更新/启用/禁用）时附加。
/// 消费方：<c>JsonRuleLoader</c> 监听该事件重新从 DB 加载规则定义实现热刷新；
/// 通过 Outbox 翻译为集成事件可跨服务通知（如多实例 Promotion API 同步刷新缓存）。
/// </summary>
public sealed class PromotionRuleDefinitionChangedEvent : DomainEventBase
{
    /// <summary>规则定义标识。</summary>
    public Guid RuleDefinitionId { get; init; }

    /// <summary>规则类型标识。</summary>
    public string RuleType { get; init; }

    /// <summary>规则版本号（变更后）。</summary>
    public string Version { get; init; }

    public PromotionRuleDefinitionChangedEvent(
        Guid ruleDefinitionId,
        string ruleType,
        string version) : base(ruleDefinitionId)
    {
        RuleDefinitionId = ruleDefinitionId;
        RuleType = ruleType;
        Version = version;
    }
}
