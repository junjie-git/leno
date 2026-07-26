using Leno.PointsMembership.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;
using PointsRuleAggregate = Leno.PointsMembership.Domain.Aggregates.PointsRule;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 积分规则仓储接口，管理 <see cref="PointsRule"/> 聚合。
/// 提供按编码查询（用于唯一性校验）与全量查询（运营端列表）。
/// </summary>
public interface IPointsRuleRepository : IRepository<PointsRuleAggregate>
{
    /// <summary>
    /// 按规则编码查询积分规则，用于创建时的唯一性校验。
    /// </summary>
    /// <param name="code">规则编码，大小写敏感。</param>
    Task<PointsRuleAggregate?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// 查询全部积分规则（含停用），按创建时间升序，供运营端管理。
    /// </summary>
    Task<List<PointsRuleAggregate>> GetAllAsync(CancellationToken ct = default);
}
