using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 跨域审计日志条目仓储接口，定义在领域层，由基础设施层实现。
/// 审计日志条目仅追加，不提供更新/删除操作。
/// </summary>
public interface IAuditLogEntryRepository
{
    /// <summary>
    /// 追加审计日志条目。
    /// </summary>
    /// <param name="entry">审计日志条目聚合。</param>
    /// <param name="ct">取消令牌。</param>
    Task AddAsync(AuditLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// 按标识获取审计日志条目。
    /// </summary>
    /// <param name="id">条目标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 分页查询审计日志条目，支持模块、操作动作、时间区间与操作人过滤。
    /// </summary>
    /// <param name="moduleName">领域模块，可空表示不限。</param>
    /// <param name="action">操作动作，可空表示不限。</param>
    /// <param name="fromTime">起始时间（UTC），可空表示不限。</param>
    /// <param name="toTime">截止时间（UTC），可空表示不限。</param>
    /// <param name="operatorId">操作人标识，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<AuditLogEntry>> QueryAsync(string? moduleName, string? action, DateTime? fromTime, DateTime? toTime, Guid? operatorId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计审计日志条目数量，支持模块、操作动作、时间区间与操作人过滤。
    /// </summary>
    Task<int> CountAsync(string? moduleName, string? action, DateTime? fromTime, DateTime? toTime, Guid? operatorId, CancellationToken ct = default);

    /// <summary>
    /// 按 EventId 查找已存在的审计日志条目（用于幂等去重）。
    /// </summary>
    /// <param name="eventId">集成事件 EventId。</param>
    /// <param name="ct">取消令牌。</param>
    Task<AuditLogEntry?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// 删除指定时间之前的审计日志条目（用于数据保留策略）。
    /// </summary>
    /// <param name="before">删除此时间之前的条目（UTC）。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> DeleteOlderThanAsync(DateTime before, CancellationToken ct = default);
}