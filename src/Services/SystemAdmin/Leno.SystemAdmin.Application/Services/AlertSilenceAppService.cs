using System.Text.Json;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 告警静默规则应用服务实现。
/// 委托 <see cref="IAlertmanagerClient"/> 与 Alertmanager 交互，映射为 DTO 返回。
/// 创建静默规则时校验匹配器非空、持续时长格式合法、原因非空。
/// </summary>
public sealed class AlertSilenceAppService : IAlertSilenceAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private const int MaxMatchersCount = 32;
    private const int MaxDurationLength = 64;
    private const int MaxReasonLength = 1000;

    private readonly IAlertmanagerClient _alertmanagerClient;
    private readonly ILogger<AlertSilenceAppService> _logger;

    public AlertSilenceAppService(IAlertmanagerClient alertmanagerClient, ILogger<AlertSilenceAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(alertmanagerClient);
        ArgumentNullException.ThrowIfNull(logger);
        _alertmanagerClient = alertmanagerClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AlertSilenceDto> CreateAsync(CreateAlertSilenceDto dto, string createdBy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException("创建人标识不可为空", nameof(createdBy));
        }

        ValidateMatchers(dto.Matchers);
        ValidateDuration(dto.Duration);
        ValidateReason(dto.Reason);

        var matchersJson = JsonSerializer.Serialize(
            dto.Matchers.Select(m => new { name = m.Name, value = m.Value, isRegex = m.IsRegex }),
            JsonOptions);

        var silence = await _alertmanagerClient.CreateSilenceAsync(matchersJson, dto.Duration, dto.Reason, createdBy, ct);

        _logger.LogInformation(
            "静默规则已创建 SilenceId={SilenceId} CreatedBy={CreatedBy} Duration={Duration}",
            silence.Id, silence.CreatedBy, silence.Duration);

        return ToDto(silence);
    }

    /// <inheritdoc />
    public async Task<AlertSilenceListResultDto> QueryAsync(CancellationToken ct = default)
    {
        var silences = await _alertmanagerClient.GetSilencesAsync(ct);

        _logger.LogInformation("查询静默规则列表 Count={Count}", silences.Count);

        return new AlertSilenceListResultDto
        {
            Items = silences.Select(ToDto).ToList()
        };
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid silenceId, CancellationToken ct = default)
    {
        if (silenceId == Guid.Empty)
        {
            throw new ArgumentException("静默规则标识不可为空", nameof(silenceId));
        }

        await _alertmanagerClient.DeleteSilenceAsync(silenceId, ct);

        _logger.LogInformation("静默规则已删除 SilenceId={SilenceId}", silenceId);
    }

    private static void ValidateMatchers(List<MatcherItemDto> matchers)
    {
        ArgumentNullException.ThrowIfNull(matchers);
        if (matchers.Count == 0)
        {
            throw new ArgumentException("匹配器不可为空");
        }
        if (matchers.Count > MaxMatchersCount)
        {
            throw new ArgumentException($"匹配器数量不可超过 {MaxMatchersCount}");
        }
        foreach (var m in matchers)
        {
            if (string.IsNullOrWhiteSpace(m.Name))
            {
                throw new ArgumentException("匹配器名称不可为空");
            }
            if (string.IsNullOrWhiteSpace(m.Value))
            {
                throw new ArgumentException("匹配器值不可为空");
            }
        }
    }

    private static void ValidateDuration(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            throw new ArgumentException("持续时长不可为空");
        }
        if (duration.Trim().Length > MaxDurationLength)
        {
            throw new ArgumentException($"持续时长描述长度不可超过 {MaxDurationLength} 字符");
        }
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("静默原因不可为空");
        }
        if (reason.Trim().Length > MaxReasonLength)
        {
            throw new ArgumentException($"静默原因长度不可超过 {MaxReasonLength} 字符");
        }
    }

    private static AlertSilenceDto ToDto(Domain.Aggregates.AlertSilence silence)
        => new()
        {
            SilenceId = silence.Id,
            Matchers = silence.Matchers,
            Duration = silence.Duration,
            Reason = silence.Reason,
            StartsAt = silence.StartsAt,
            EndsAt = silence.EndsAt,
            CreatedBy = silence.CreatedBy,
            CreatedAt = silence.CreatedAt,
            IsExpired = silence.IsExpired()
        };
}
