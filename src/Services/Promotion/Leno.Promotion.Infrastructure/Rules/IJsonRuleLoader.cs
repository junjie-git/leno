using Leno.Promotion.Domain.Rules;

namespace Leno.Promotion.Infrastructure.Rules;

/// <summary>
/// 规则定义加载器接口，从 <see cref="Leno.Promotion.Domain.Aggregates.PromotionRuleDefinition"/> 聚合的
/// <see cref="Leno.Promotion.Domain.Aggregates.PromotionRuleDefinition.DefinitionJson"/> 反序列化为 <see cref="JsonRuleDefinition"/>，
/// 缓存供 <see cref="IPromotionRule"/> 实现读取。
/// 启动时调用 <see cref="LoadAsync"/> 加载；规则定义变更后调用 <see cref="ReloadAsync"/> 热刷新。
/// </summary>
public interface IJsonRuleLoader
{
    /// <summary>
    /// 启动加载：从 DB 查询所有启用规则定义并反序列化缓存。
    /// 通常由 <c>IHostedService.StartAsync</c> 调用。
    /// </summary>
    Task LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// 热刷新：重新从 DB 加载所有启用规则定义覆盖缓存。
    /// 通常由 <c>PromotionRuleDefinitionAppService</c> 在 Update/Enable/Disable 后调用，
    /// 也可由进程内事件订阅器在 <see cref="Leno.Promotion.Domain.Events.PromotionRuleDefinitionChangedEvent"/> 触发时调用。
    /// </summary>
    Task ReloadAsync(CancellationToken ct = default);

    /// <summary>
    /// 按规则类型查询缓存的 <see cref="JsonRuleDefinition"/>。
    /// 未加载或不存在时返回 <c>null</c>，规则实现应回退到默认行为（避免阻塞评估）。
    /// </summary>
    /// <param name="ruleType">规则类型标识（与 <see cref="IPromotionRule.RuleType"/> 对齐）。</param>
    /// <returns>缓存的规则定义；若未加载则返回 <c>null</c>。</returns>
    JsonRuleDefinition? GetDefinition(string ruleType);

    /// <summary>
    /// 当前缓存版本号（启动/每次刷新递增），用于诊断与可观测。
    /// </summary>
    long CachedVersion { get; }

    /// <summary>
    /// 缓存加载时间（UTC），用于诊断刷新是否生效。
    /// </summary>
    DateTime? LoadedAt { get; }
}
