using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Application.Services;

/// <summary>
/// 频率限制管理应用服务实现（运营端）。
/// 提供频率限制规则的查询与更新功能。
/// </summary>
public sealed class RateLimitAppService : IRateLimitAppService
{
    private readonly ILogger<RateLimitAppService> _logger;

    // 默认频率限制规则
    private static readonly Dictionary<NotificationChannel, RateLimitConfigDto> DefaultConfigs = new()
    {
        [NotificationChannel.Email] = new RateLimitConfigDto
        {
            Channel = NotificationChannel.Email,
            HourlyLimit = 10,
            DailyLimit = null,
            Enabled = true
        },
        [NotificationChannel.Sms] = new RateLimitConfigDto
        {
            Channel = NotificationChannel.Sms,
            HourlyLimit = 5,
            DailyLimit = 20,
            Enabled = true
        },
        [NotificationChannel.InApp] = new RateLimitConfigDto
        {
            Channel = NotificationChannel.InApp,
            HourlyLimit = int.MaxValue,
            DailyLimit = null,
            Enabled = false
        }
    };

    public RateLimitAppService(ILogger<RateLimitAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<RateLimitConfigDto> GetRateLimitAsync(NotificationChannel channel, CancellationToken ct = default)
    {
        if (DefaultConfigs.TryGetValue(channel, out var config))
        {
            return Task.FromResult(config);
        }

        return Task.FromResult(new RateLimitConfigDto
        {
            Channel = channel,
            HourlyLimit = 0,
            Enabled = false
        });
    }

    /// <inheritdoc />
    public Task UpdateRateLimitAsync(Guid operatorId, NotificationChannel channel, SaveRateLimitConfigDto dto, CancellationToken ct = default)
    {
        if (DefaultConfigs.TryGetValue(channel, out var config))
        {
            if (dto.HourlyLimit.HasValue) config.HourlyLimit = dto.HourlyLimit.Value;
            if (dto.DailyLimit.HasValue) config.DailyLimit = dto.DailyLimit.Value;
            if (dto.Enabled.HasValue) config.Enabled = dto.Enabled.Value;
        }

        // 审计日志
        _logger.LogWarning("AUDIT: 操作员 {OperatorId} 更新了渠道 {Channel} 的频率限制：HourlyLimit={HourlyLimit} DailyLimit={DailyLimit} Enabled={Enabled}",
            operatorId, channel, dto.HourlyLimit, dto.DailyLimit, dto.Enabled);

        return Task.CompletedTask;
    }
}