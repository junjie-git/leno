using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 告警管理应用服务实现。
/// 委托 <see cref="IAlertmanagerClient"/> 与 Alertmanager 交互，映射为 DTO 返回。
/// 查询参数规范化：page/pageSize 下界校验、时间范围合法性校验。
/// </summary>
public sealed class AlertAppService : IAlertAppService
{
    private const int MaxPageSize = 200;
    private const int MaxTimeRangeDays = 30;

    private readonly IAlertmanagerClient _alertmanagerClient;
    private readonly ILogger<AlertAppService> _logger;

    public AlertAppService(IAlertmanagerClient alertmanagerClient, ILogger<AlertAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(alertmanagerClient);
        ArgumentNullException.ThrowIfNull(logger);
        _alertmanagerClient = alertmanagerClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AlertListResultDto> QueryAsync(
        string? moduleName,
        AlertSeverity? severity,
        AlertStatus? status,
        DateTime? start,
        DateTime? endTime,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        ValidateTimeRange(start, endTime);

        var filter = new AlertQueryFilter
        {
            Module = string.IsNullOrWhiteSpace(moduleName) ? null : moduleName.Trim(),
            Severity = severity,
            Status = status,
            Start = start,
            End = endTime,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        };

        var result = await _alertmanagerClient.GetAlertsAsync(filter, ct);

        _logger.LogInformation(
            "查询告警事件 Module={Module} Severity={Severity} Status={Status} Start={Start} End={End} Page={Page} PageSize={PageSize} Total={Total}",
            filter.Module, filter.Severity, filter.Status, filter.Start, filter.End, normalizedPage, normalizedPageSize, result.Total);

        return new AlertListResultDto
        {
            Items = result.Items.Select(ToListDto).ToList(),
            Total = result.Total,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        };
    }

    /// <inheritdoc />
    public async Task<AlertDetailDto?> GetByIdAsync(Guid alertId, CancellationToken ct = default)
    {
        if (alertId == Guid.Empty)
        {
            throw new ArgumentException("告警标识不可为空", nameof(alertId));
        }

        var alert = await _alertmanagerClient.GetAlertAsync(alertId, ct);
        if (alert is null)
        {
            _logger.LogWarning("告警 {AlertId} 不存在", alertId);
            return null;
        }

        _logger.LogInformation("获取告警详情 AlertId={AlertId} Name={Name}", alert.Id, alert.Name);
        return ToDetailDto(alert);
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(Guid alertId, string operatorId, string? comment, CancellationToken ct = default)
    {
        if (alertId == Guid.Empty)
        {
            throw new ArgumentException("告警标识不可为空", nameof(alertId));
        }
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ArgumentException("操作者标识不可为空", nameof(operatorId));
        }

        await _alertmanagerClient.AcknowledgeAlertAsync(alertId, operatorId, comment, ct);

        _logger.LogInformation(
            "告警 {AlertId} 已由 {OperatorId} 确认，备注：{Comment}",
            alertId, operatorId, comment ?? "(无)");
    }

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? 20 : (pageSize > MaxPageSize ? MaxPageSize : pageSize);

    private static void ValidateTimeRange(DateTime? start, DateTime? end)
    {
        if (start.HasValue && end.HasValue && end.Value < start.Value)
        {
            throw new ArgumentException("结束时间不可早于起始时间");
        }
        if (start.HasValue && end.HasValue)
        {
            var range = end.Value - start.Value;
            if (range.TotalDays > MaxTimeRangeDays)
            {
                throw new ArgumentException($"时间范围不可超过 {MaxTimeRangeDays} 天");
            }
        }
    }

    private static AlertDto ToListDto(Domain.Aggregates.Alert alert)
        => new()
        {
            AlertId = alert.Id,
            Name = alert.Name,
            Module = alert.Module,
            Severity = alert.Severity,
            Status = alert.Status,
            TriggeredAt = alert.TriggeredAt,
            DurationSeconds = alert.DurationSeconds,
            Summary = alert.Summary
        };

    private static AlertDetailDto ToDetailDto(Domain.Aggregates.Alert alert)
        => new()
        {
            AlertId = alert.Id,
            Name = alert.Name,
            Module = alert.Module,
            Severity = alert.Severity,
            Status = alert.Status,
            TriggeredAt = alert.TriggeredAt,
            DurationSeconds = alert.DurationSeconds,
            Labels = new Dictionary<string, string>(alert.Labels, StringComparer.Ordinal),
            Annotations = new Dictionary<string, string>(alert.Annotations, StringComparer.Ordinal),
            RelatedMetric = alert.RelatedMetric,
            Summary = alert.Summary,
            Description = alert.Description,
            AcknowledgedAt = alert.AcknowledgedAt,
            AcknowledgedBy = alert.AcknowledgedBy,
            AcknowledgeComment = alert.AcknowledgeComment
        };
}
