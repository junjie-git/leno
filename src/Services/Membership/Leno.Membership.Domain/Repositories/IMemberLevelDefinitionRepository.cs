using Leno.Membership.Domain.Aggregates.MemberLevelDefinition;
using Leno.SharedKernel.Abstractions;
using MemberLevelDefinitionAggregate = Leno.Membership.Domain.Aggregates.MemberLevelDefinition.MemberLevelDefinition;

namespace Leno.Membership.Domain.Repositories;

/// <summary>
/// 会员等级定义仓储接口，管理 <see cref="MemberLevelDefinition"/> 聚合。
/// 供 MemberLevelEvaluationJob 加载全部等级定义并转换为值对象供评估。
/// </summary>
public interface IMemberLevelDefinitionRepository : IRepository<MemberLevelDefinitionAggregate>
{
    /// <summary>
    /// 按等级编号查询会员等级定义。
    /// </summary>
    /// <param name="level">等级编号（0-4）。</param>
    Task<MemberLevelDefinitionAggregate?> GetByLevelAsync(int level, CancellationToken ct = default);

    /// <summary>
    /// 查询所有会员等级定义，按 MinGrowthValue 升序，供等级评估。
    /// </summary>
    Task<List<MemberLevelDefinitionAggregate>> GetAllAsync(CancellationToken ct = default);
}
