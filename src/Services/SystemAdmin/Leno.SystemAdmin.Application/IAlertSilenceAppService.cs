using Leno.SystemAdmin.Application.DTOs;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 告警静默规则应用服务接口，封装静默规则 CRUD 用例。
/// 委托 <see cref="Domain.Services.IAlertmanagerClient"/> 与 Alertmanager 交互。
/// </summary>
public interface IAlertSilenceAppService
{
    /// <summary>创建静默规则。</summary>
    Task<AlertSilenceDto> CreateAsync(CreateAlertSilenceDto dto, string createdBy, CancellationToken ct = default);

    /// <summary>查询静默规则列表。</summary>
    Task<AlertSilenceListResultDto> QueryAsync(CancellationToken ct = default);

    /// <summary>按 ID 删除静默规则。</summary>
    Task DeleteAsync(Guid silenceId, CancellationToken ct = default);
}
