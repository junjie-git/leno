using Leno.UserAuth.Domain.Aggregates;

namespace Leno.UserAuth.Domain.Repositories;

/// <summary>
/// 审计日志仓储接口，仅承载事务内写入。
/// 查询方法仅供 BC11 系统管理域（F-SYS-009）跨域只读聚合内部调用，本域不对外暴露审计日志查询 HTTP API。
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>事务内写入审计日志。</summary>
    Task AddAsync(AuditLog auditLog, CancellationToken ct = default);

    /// <summary>按操作人/操作类型/时间区间/资源类型分页查询（跨域只读聚合内部调用）。</summary>
    Task<(IReadOnlyList<AuditLog> Items, int Total)> QueryAsync(
        Guid? operatorId = null,
        string? action = null,
        string? resourceType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    /// <summary>按标识查询审计日志详情。</summary>
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
