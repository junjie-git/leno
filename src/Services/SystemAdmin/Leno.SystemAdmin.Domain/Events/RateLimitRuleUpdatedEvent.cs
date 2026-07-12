using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Events;

/// <summary>
/// 限流规则变更领域事件，当规则创建、更新、启用或停用时发布。
/// 消费方：网关层热加载最新限流策略。
/// </summary>
public sealed class RateLimitRuleUpdatedEvent : DomainEventBase
{
    /// <summary>变更的限流规则标识。</summary>
    public Guid RuleId => AggregateId;

    public RateLimitRuleUpdatedEvent(Guid ruleId) : base(ruleId)
    {
    }
}