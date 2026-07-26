using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// Outbox 归档历史仓储接口，定义在领域层，由基础设施层实现。
/// 支持按上下文分页查询归档历史。
/// </summary>
public interface IOutboxArchiveRecordRepository : IRepository<OutboxArchiveRecord>
{
    /// <summary>
    /// 按上下文分页查询归档历史。
    /// </summary>
    /// <param name="context">限界上下文；为空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<OutboxArchiveRecord>> QueryAsync(string? context, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计归档历史数量。
    /// </summary>
    /// <param name="context">限界上下文；为空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(string? context, CancellationToken ct = default);

    /// <summary>
    /// 按上下文获取最近一次归档时间。
    /// </summary>
    /// <param name="context">限界上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>最近归档时间（UTC）；无归档记录返回 null。</returns>
    Task<DateTime?> GetLastArchivedAtAsync(string context, CancellationToken ct = default);
}
