using Leno.Promotion.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;
using PromotionRuleDefinitionAggregate = Leno.Promotion.Domain.Aggregates.PromotionRuleDefinition;

namespace Leno.Promotion.Domain.Repositories;

/// <summary>
/// 促销规则定义仓储接口，管理 <see cref="PromotionRuleDefinition"/> 聚合。
/// <c>JsonRuleLoader</c> 启动时与热刷新时通过本接口查询所有启用规则定义。
/// </summary>
public interface IPromotionRuleDefinitionRepository : IRepository<PromotionRuleDefinitionAggregate>
{
    /// <summary>
    /// 查询所有启用的规则定义，按 <see cref="PromotionRuleDefinition.Priority"/> 升序返回。
    /// <c>JsonRuleLoader</c> 启动与热刷新时调用。
    /// </summary>
    Task<List<PromotionRuleDefinitionAggregate>> GetEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// 按规则类型查询启用的规则定义（同类型唯一，至多 1 条；如有多条取最新创建者）。
    /// </summary>
    Task<PromotionRuleDefinitionAggregate?> GetByRuleTypeAsync(
        string ruleType,
        CancellationToken ct = default);

    /// <summary>
    /// 按规则类型集合批量查询启用的规则定义，单次 DB 往返。
    /// </summary>
    Task<List<PromotionRuleDefinitionAggregate>> GetByRuleTypesAsync(
        IEnumerable<string> ruleTypes,
        CancellationToken ct = default);
}
