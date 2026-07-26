using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 告警管理应用服务接口，封装告警查询、详情、确认用例。
/// 委托 <see cref="Domain.Services.IAlertmanagerClient"/> 与 Alertmanager 交互。
/// </summary>
public interface IAlertAppService
{
    /// <summary>分页查询告警事件，支持 module/severity/status/时间范围筛选。</summary>
    Task<AlertListResultDto> QueryAsync(
        string? moduleName,
        AlertSeverity? severity,
        AlertStatus? status,
        DateTime? start,
        DateTime? endTime,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>按 ID 获取告警详情。</summary>
    Task<AlertDetailDto?> GetByIdAsync(Guid alertId, CancellationToken ct = default);

    /// <summary>确认告警。</summary>
    Task AcknowledgeAsync(Guid alertId, string operatorId, string? comment, CancellationToken ct = default);
}
