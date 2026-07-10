using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 定时任务仓储接口，定义在领域层，由基础设施层实现。
/// 支持查询启用任务与按名称、状态分页查询，写操作由工作单元统一提交。
/// </summary>
public interface IScheduledTaskRepository : IRepository<ScheduledTask>
{
    /// <summary>
    /// 获取全部启用的定时任务。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    Task<List<ScheduledTask>> GetEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// 分页查询定时任务，支持名称与状态过滤。
    /// </summary>
    /// <param name="name">名称关键词，可空。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<ScheduledTask>> QueryAsync(string? name, ScheduledTaskStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计定时任务数量，支持名称与状态过滤。
    /// </summary>
    /// <param name="name">名称关键词，可空。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(string? name, ScheduledTaskStatus? status, CancellationToken ct = default);
}
