using System.Globalization;
using System.Text;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 登录日志查询应用服务实现。
/// 复用 AuditLogAppService 的 CSV 流式导出模式，限制单次最大 10 万条。
/// </summary>
public sealed class LoginLogAppService : ILoginLogAppService
{
    private const string CsvHeader = "id,loginAt,username,ipAddress,geoLocation,browser,os,result,failureReason,durationMs,traceId";
    private const int MaxExportCount = 100_000;

    private readonly ILoginLogRepository _loginLogRepository;
    private readonly ILogger<LoginLogAppService> _logger;

    public LoginLogAppService(
        ILoginLogRepository loginLogRepository,
        ILogger<LoginLogAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(loginLogRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _loginLogRepository = loginLogRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginLogListResultDto> QueryAsync(LoginLogQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        NormalizePaging(query);

        var (items, total) = await _loginLogRepository.QueryAsync(query, ct);
        return new LoginLogListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<LoginLogDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var log = await _loginLogRepository.GetByIdAsync(id, ct);
        return log is null ? null : ToDto(log);
    }

    /// <inheritdoc />
    public async Task<string> ExportAsync(LoginLogQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sb = new StringBuilder();
        sb.Append(CsvHeader).Append('\n');

        var exported = 0;
        await foreach (var log in _loginLogRepository.StreamAsync(query, MaxExportCount + 1, ct))
        {
            if (exported >= MaxExportCount)
            {
                _logger.LogWarning("登录日志导出已达到上限 {MaxCount} 条，超出部分请缩小时间范围分批导出", MaxExportCount);
                break;
            }

            sb.Append(log.Id.ToString());
            sb.Append(',');
            sb.Append(log.LoginAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(EscapeCsvField(log.Username));
            sb.Append(',');
            sb.Append(EscapeCsvField(log.IpAddress));
            sb.Append(',');
            sb.Append(EscapeCsvField(log.GeoLocation ?? string.Empty));
            sb.Append(',');
            sb.Append(EscapeCsvField(log.Browser));
            sb.Append(',');
            sb.Append(EscapeCsvField(log.Os));
            sb.Append(',');
            sb.Append(log.Result == LoginResult.Success ? "Success" : "Failed");
            sb.Append(',');
            sb.Append(EscapeCsvField(log.FailureReason ?? string.Empty));
            sb.Append(',');
            sb.Append(log.DurationMs.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(EscapeCsvField(log.TraceId));
            sb.Append('\n');

            exported++;
        }

        _logger.LogInformation("登录日志已导出：{Count} 条", exported);
        return sb.ToString();
    }

    private static void NormalizePaging(LoginLogQuery query)
    {
        if (query.Page < 1) query.Page = 1;
        if (query.PageSize < 1) query.PageSize = 20;
        if (query.PageSize > 200) query.PageSize = 200;
    }

    private static string EscapeCsvField(string field)
    {
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return field;
        }
        return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static LoginLogDto ToDto(LoginLog entity)
        => new()
        {
            Id = entity.Id,
            Username = entity.Username,
            UserId = entity.UserId,
            IpAddress = entity.IpAddress,
            GeoLocation = entity.GeoLocation,
            Browser = entity.Browser,
            Os = entity.Os,
            Result = entity.Result,
            FailureReason = entity.FailureReason,
            DurationMs = entity.DurationMs,
            UserAgent = entity.UserAgent,
            DeviceFingerprint = entity.DeviceFingerprint,
            RefererUrl = entity.RefererUrl,
            TraceId = entity.TraceId,
            LoginAt = entity.LoginAt
        };
}
