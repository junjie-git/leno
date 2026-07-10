using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 特性开关仓储接口，定义在领域层，由基础设施层实现。
/// 支持按键、状态查询，写操作由工作单元统一提交。
/// </summary>
public interface IFeatureFlagRepository : IRepository<FeatureFlag>
{
    /// <summary>
    /// 按开关键获取开关。
    /// </summary>
    /// <param name="key">开关键。</param>
    /// <param name="ct">取消令牌。</param>
    Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 分页查询开关，支持键与状态过滤。
    /// </summary>
    /// <param name="key">键关键词，可空。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<FeatureFlag>> QueryAsync(string? key, FeatureFlagStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计开关数量，支持键与状态过滤。
    /// </summary>
    /// <param name="key">键关键词，可空。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(string? key, FeatureFlagStatus? status, CancellationToken ct = default);
}
