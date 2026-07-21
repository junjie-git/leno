using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 审计日志仓储接口，定义在领域层，由基础设施层实现。
/// 审计日志仅追加，不提供更新/删除操作，故不继承 <see cref="Leno.SharedKernel.Abstractions.IRepository{T}"/>。
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// 追加审计日志。
    /// </summary>
    /// <param name="log">审计日志聚合。</param>
    /// <param name="ct">取消令牌。</param>
    Task AddAsync(AuditLog log, CancellationToken ct = default);

    /// <summary>
    /// 按标识获取审计日志。
    /// </summary>
    /// <param name="id">日志标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 分页查询审计日志，支持运营人员、资源类型与时间区间过滤。
    /// </summary>
    /// <param name="operatorId">运营人员标识，可空表示不限。</param>
    /// <param name="resourceType">资源类型，可空表示不限。</param>
    /// <param name="fromTime">起始时间（UTC），可空表示不限。</param>
    /// <param name="toTime">截止时间（UTC），可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<AuditLog>> QueryAsync(Guid? operatorId, string? resourceType, DateTime? fromTime, DateTime? toTime, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计审计日志数量，支持运营人员、资源类型与时间区间过滤。
    /// </summary>
    /// <param name="operatorId">运营人员标识，可空表示不限。</param>
    /// <param name="resourceType">资源类型，可空表示不限。</param>
    /// <param name="fromTime">起始时间（UTC），可空表示不限。</param>
    /// <param name="toTime">截止时间（UTC），可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(Guid? operatorId, string? resourceType, DateTime? fromTime, DateTime? toTime, CancellationToken ct = default);

    /// <summary>
    /// 流式拉取审计日志，按时间倒序。
    /// 使用 <c>AsNoTracking().AsAsyncEnumerable()</c> 避免一次性加载到内存导致 OOM。
    /// 调用方应限制最大条数（例如 10 万），超出部分应分批导出。
    /// </summary>
    /// <param name="operatorId">运营人员标识，可空表示不限。</param>
    /// <param name="resourceType">资源类型，可空表示不限。</param>
    /// <param name="fromTime">起始时间（UTC），可空表示不限。</param>
    /// <param name="toTime">截止时间（UTC），可空表示不限。</param>
    /// <param name="maxCount">最大返回条数，超出部分不返回，调用方应分批导出。默认 10 万。</param>
    /// <param name="ct">取消令牌。</param>
    IAsyncEnumerable<AuditLog> StreamAsync(
        Guid? operatorId,
        string? resourceType,
        DateTime? fromTime,
        DateTime? toTime,
        int maxCount = 100_000,
        CancellationToken ct = default);
}
