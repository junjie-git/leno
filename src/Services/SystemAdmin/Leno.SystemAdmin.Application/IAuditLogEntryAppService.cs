using Leno.SystemAdmin.Application.DTOs;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 跨域审计日志条目查询应用服务接口。
/// 日志条目仅追加，本接口仅提供查询能力。
/// </summary>
public interface IAuditLogEntryAppService
{
    /// <summary>分页查询审计日志条目，支持模块、操作动作、时间区间与操作人过滤。</summary>
    Task<AuditLogEntryListResultDto> QueryAsync(string? moduleName, string? action, DateTime? fromTime, DateTime? toTime, Guid? operatorId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>按标识获取审计日志条目详情。</summary>
    Task<AuditLogEntryDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}