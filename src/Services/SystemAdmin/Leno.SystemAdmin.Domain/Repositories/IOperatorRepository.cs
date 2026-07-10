using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 运营人员仓储接口，定义在领域层，由基础设施层实现。
/// 支持按用户账号、角色、状态查询，写操作由工作单元统一提交。
/// </summary>
public interface IOperatorRepository : IRepository<Operator>
{
    /// <summary>
    /// 按用户域账号标识获取运营人员。
    /// </summary>
    /// <param name="userId">用户域账号标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<Operator?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询运营人员，支持角色与状态过滤。
    /// </summary>
    /// <param name="role">角色过滤，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<Operator>> QueryAsync(OperatorRole? role, OperatorStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计运营人员数量，支持角色与状态过滤。
    /// </summary>
    /// <param name="role">角色过滤，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(OperatorRole? role, OperatorStatus? status, CancellationToken ct = default);
}
