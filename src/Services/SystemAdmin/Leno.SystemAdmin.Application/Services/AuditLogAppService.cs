using System.Globalization;
using System.Text;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 审计与操作日志查询应用服务实现。
/// 日志仅追加，本服务仅提供查询与 CSV 导出能力。
/// </summary>
public sealed class AuditLogAppService : IAuditLogAppService
{
    private const string AuditLogCsvHeader = "LogId,OperatorId,Action,ResourceType,ResourceId,ResponseStatus,OccurredAt";

    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IOperationLogRepository _operationLogRepository;
    private readonly ILogger<AuditLogAppService> _logger;

    public AuditLogAppService(
        IAuditLogRepository auditLogRepository,
        IOperationLogRepository operationLogRepository,
        ILogger<AuditLogAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(auditLogRepository);
        ArgumentNullException.ThrowIfNull(operationLogRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _auditLogRepository = auditLogRepository;
        _operationLogRepository = operationLogRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuditLogListResultDto> QueryAuditLogsAsync(Guid? operatorId, string? resourceType, DateTime? fromTime, DateTime? toTime, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _auditLogRepository.QueryAsync(operatorId, resourceType, fromTime, toTime, page, pageSize, ct);
        var total = await _auditLogRepository.CountAsync(operatorId, resourceType, fromTime, toTime, ct);

        return new AuditLogListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<OperationLogListResultDto> QueryOperationLogsAsync(Guid? operatorId, string? moduleName, DateTime? fromTime, DateTime? toTime, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _operationLogRepository.QueryAsync(operatorId, moduleName, fromTime, toTime, page, pageSize, ct);
        var total = await _operationLogRepository.CountAsync(operatorId, moduleName, fromTime, toTime, ct);

        return new OperationLogListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<string> ExportAuditLogsAsync(Guid? operatorId, string? resourceType, DateTime? fromTime, DateTime? toTime, CancellationToken ct = default)
    {
        // 流式拉取审计日志，限制单次最大导出 10 万条，超出部分应分批导出
        const int maxExportCount = 100_000;

        var sb = new StringBuilder();
        sb.Append(AuditLogCsvHeader).Append('\n');

        var exported = 0;
        await foreach (var log in _auditLogRepository.StreamAsync(operatorId, resourceType, fromTime, toTime, maxExportCount + 1, ct))
        {
            if (exported >= maxExportCount)
            {
                _logger.LogWarning(
                    "审计日志导出已达到上限 {MaxCount} 条，超出部分请缩小时间范围分批导出 OperatorId={OperatorId} ResourceType={ResourceType}",
                    maxExportCount, operatorId, resourceType);
                break;
            }

            sb.Append(log.LogId.ToString());
            sb.Append(',');
            sb.Append(log.OperatorId.ToString());
            sb.Append(',');
            sb.Append(EscapeCsvField(log.Action));
            sb.Append(',');
            sb.Append(EscapeCsvField(log.ResourceType));
            sb.Append(',');
            sb.Append(EscapeCsvField(log.ResourceId));
            sb.Append(',');
            sb.Append(log.ResponseStatus.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(log.OccurredAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            sb.Append('\n');

            exported++;
        }

        _logger.LogInformation("审计日志已导出：{Count} 条", exported);
        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return field;
        }

        return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static AuditLogDto ToDto(AuditLog entity)
        => new()
        {
            LogId = entity.LogId,
            OperatorId = entity.OperatorId,
            Action = entity.Action,
            ResourceType = entity.ResourceType,
            ResourceId = entity.ResourceId,
            RequestSummary = entity.RequestSummary,
            ResponseStatus = entity.ResponseStatus,
            IpAddress = entity.IpAddress,
            TraceId = entity.TraceId,
            OccurredAt = entity.OccurredAt
        };

    private static OperationLogDto ToDto(OperationLog entity)
        => new()
        {
            LogId = entity.LogId,
            OperatorId = entity.OperatorId,
            OperationType = entity.OperationType,
            Module = entity.Module,
            Description = entity.Description,
            BeforeSnapshot = entity.BeforeSnapshot,
            AfterSnapshot = entity.AfterSnapshot,
            IpAddress = entity.IpAddress,
            OccurredAt = entity.OccurredAt
        };
}
