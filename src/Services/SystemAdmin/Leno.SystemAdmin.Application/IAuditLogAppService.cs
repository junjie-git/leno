using Leno.SystemAdmin.Application.DTOs;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 审计与操作日志查询应用服务接口。
/// 日志仅追加，本接口仅提供查询与导出能力。
/// </summary>
public interface IAuditLogAppService
{
    /// <summary>分页查询审计日志，支持运营人员、资源类型与时间区间过滤。</summary>
    Task<AuditLogListResultDto> QueryAuditLogsAsync(Guid? operatorId, string? resourceType, DateTime? fromTime, DateTime? toTime, int page, int pageSize, CancellationToken ct = default);

    /// <summary>分页查询操作日志，支持运营人员、模块与时间区间过滤。</summary>
    Task<OperationLogListResultDto> QueryOperationLogsAsync(Guid? operatorId, string? moduleName, DateTime? fromTime, DateTime? toTime, int page, int pageSize, CancellationToken ct = default);

    /// <summary>导出审计日志为 CSV 字符串，支持运营人员、资源类型与时间区间过滤。</summary>
    Task<string> ExportAuditLogsAsync(Guid? operatorId, string? resourceType, DateTime? fromTime, DateTime? toTime, CancellationToken ct = default);
}
