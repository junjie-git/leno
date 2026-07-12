using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 限流规则仓储接口，定义在领域层，由基础设施层实现。
/// 继承泛型仓储基接口，支持增删改查操作。
/// </summary>
public interface IRateLimitRuleRepository : IRepository<RateLimitRule>
{
    /// <summary>
    /// 获取所有启用的限流规则（供策略解析器与网关热加载）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    Task<List<RateLimitRule>> GetAllEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// 分页查询限流规则，支持 API 路径与启用状态过滤。
    /// </summary>
    /// <param name="targetApi">目标 API 路径，可空表示不限。</param>
    /// <param name="enabled">启用状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<RateLimitRule>> QueryAsync(string? targetApi, bool? enabled, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计限流规则数量，支持 API 路径与启用状态过滤。
    /// </summary>
    Task<int> CountAsync(string? targetApi, bool? enabled, CancellationToken ct = default);
}