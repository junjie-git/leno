using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 告警查询过滤条件，封装 module/severity/status/时间范围筛选参数。
/// </summary>
public sealed class AlertQueryFilter
{
    public string? Module { get; init; }
    public AlertSeverity? Severity { get; init; }
    public AlertStatus? Status { get; init; }
    public DateTime? Start { get; init; }
    public DateTime? End { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Alertmanager 客户端抽象接口，封装对 Alertmanager HTTP API 的访问。
/// 默认实现 <see cref="Leno.SystemAdmin.Infrastructure.Services.HttpAlertmanagerClient"/> 基于 HTTP 调用。
/// 抽象便于测试时注入内存实现，也便于未来切换到其他告警源（如 VictoriaMetrics / Grafana OnCall）。
/// </summary>
public interface IAlertmanagerClient
{
    /// <summary>
    /// 分页查询告警事件，支持 module/severity/status/时间范围过滤。
    /// </summary>
    /// <param name="filter">查询过滤条件。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>告警列表与总数。</returns>
    Task<AlertQueryResult> GetAlertsAsync(AlertQueryFilter filter, CancellationToken ct = default);

    /// <summary>
    /// 按 ID 获取告警事件详情（含标签/注释/关联指标）。
    /// </summary>
    /// <param name="alertId">告警标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>告警详情；不存在返回 null。</returns>
    Task<Alert?> GetAlertAsync(Guid alertId, CancellationToken ct = default);

    /// <summary>
    /// 确认告警，向 Alertmanager 推送 acknowledge 操作。
    /// </summary>
    /// <param name="alertId">告警标识。</param>
    /// <param name="operatorId">操作者标识。</param>
    /// <param name="comment">确认备注，可空。</param>
    /// <param name="ct">取消令牌。</param>
    Task AcknowledgeAlertAsync(Guid alertId, string operatorId, string? comment, CancellationToken ct = default);

    /// <summary>
    /// 创建静默规则。
    /// </summary>
    /// <param name="matchersJson">匹配器 JSON 数组。</param>
    /// <param name="duration">持续时长描述，如 "2h"。</param>
    /// <param name="reason">静默原因。</param>
    /// <param name="createdBy">创建人标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已创建的静默规则。</returns>
    Task<AlertSilence> CreateSilenceAsync(string matchersJson, string duration, string reason, string createdBy, CancellationToken ct = default);

    /// <summary>
    /// 查询静默规则列表。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>静默规则列表。</returns>
    Task<List<AlertSilence>> GetSilencesAsync(CancellationToken ct = default);

    /// <summary>
    /// 按 ID 删除静默规则。
    /// </summary>
    /// <param name="silenceId">静默规则标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task DeleteSilenceAsync(Guid silenceId, CancellationToken ct = default);
}

/// <summary>
/// 告警查询结果，包含当前页数据与总数。
/// </summary>
public sealed class AlertQueryResult
{
    public List<Alert> Items { get; init; } = new();
    public int Total { get; init; }
}
