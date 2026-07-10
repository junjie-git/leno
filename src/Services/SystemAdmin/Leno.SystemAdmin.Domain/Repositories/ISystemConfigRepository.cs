using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 系统配置仓储接口，定义在领域层，由基础设施层实现。
/// 支持按键、分组、状态查询，写操作由工作单元统一提交。
/// </summary>
public interface ISystemConfigRepository : IRepository<SystemConfig>
{
    /// <summary>
    /// 按配置键获取配置。
    /// </summary>
    /// <param name="key">配置键。</param>
    /// <param name="ct">取消令牌。</param>
    Task<SystemConfig?> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 按分组查询配置列表。
    /// </summary>
    /// <param name="group">配置分组，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<SystemConfig>> QueryByGroupAsync(string? group, CancellationToken ct = default);

    /// <summary>
    /// 分页查询配置，支持键、分组、状态过滤。
    /// </summary>
    /// <param name="key">键关键词，可空。</param>
    /// <param name="group">分组，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<SystemConfig>> QueryAsync(string? key, string? group, ConfigStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计配置数量，支持键、分组、状态过滤。
    /// </summary>
    /// <param name="key">键关键词，可空。</param>
    /// <param name="group">分组，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(string? key, string? group, ConfigStatus? status, CancellationToken ct = default);
}
