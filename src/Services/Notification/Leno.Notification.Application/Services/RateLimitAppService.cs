using System.Collections.Concurrent;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Application.Services;

/// <summary>
/// 频率限制管理应用服务实现（运营端）。
/// 提供频率限制规则的查询与更新功能。
/// 配置持久化到 DB（notification_rate_limit_configs 表），用 ConcurrentDictionary 进程内缓存。
/// </summary>
public sealed class RateLimitAppService : IRateLimitAppService
{
    private readonly INotificationRateLimitConfigRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RateLimitAppService> _logger;

    /// <summary>进程内缓存，避免每次查询都打 DB。Key: NotificationChannel。</summary>
    private static readonly ConcurrentDictionary<NotificationChannel, RateLimitConfigDto> Cache = new();

    /// <summary>默认频率限制规则（首次访问或 DB 无记录时回填）。</summary>
    private static readonly IReadOnlyDictionary<NotificationChannel, RateLimitConfigDto> DefaultConfigs = new Dictionary<NotificationChannel, RateLimitConfigDto>
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

    public RateLimitAppService(
        INotificationRateLimitConfigRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<RateLimitAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RateLimitConfigDto> GetRateLimitAsync(NotificationChannel channel, CancellationToken ct = default)
    {
        if (Cache.TryGetValue(channel, out var cached))
        {
            return CloneDto(cached);
        }

        var aggregate = await _repository.GetByChannelAsync(channel, ct);
        if (aggregate is not null)
        {
            var dto = ToDto(aggregate);
            Cache[channel] = dto;
            return CloneDto(dto);
        }

        // DB 无记录：回填默认值并持久化，保证多实例一致
        if (DefaultConfigs.TryGetValue(channel, out var defaultConfig))
        {
            var newAggregate = NotificationRateLimitConfig.Create(
                Guid.NewGuid(),
                channel,
                defaultConfig.HourlyLimit,
                defaultConfig.DailyLimit,
                defaultConfig.Enabled);
            await _repository.AddAsync(newAggregate, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            var newDto = ToDto(newAggregate);
            Cache[channel] = newDto;
            return CloneDto(newDto);
        }

        return new RateLimitConfigDto
        {
            Channel = channel,
            HourlyLimit = 0,
            Enabled = false
        };
    }

    /// <inheritdoc />
    public async Task UpdateRateLimitAsync(Guid operatorId, NotificationChannel channel, SaveRateLimitConfigDto dto, CancellationToken ct = default)
    {
        var aggregate = await _repository.GetByChannelAsync(channel, ct);
        if (aggregate is null)
        {
            // DB 无记录：用默认值创建后再应用本次更新
            DefaultConfigs.TryGetValue(channel, out var defaultConfig);
            aggregate = NotificationRateLimitConfig.Create(
                Guid.NewGuid(),
                channel,
                defaultConfig?.HourlyLimit ?? 0,
                defaultConfig?.DailyLimit,
                defaultConfig?.Enabled ?? false);
            await _repository.AddAsync(aggregate, ct);
        }

        aggregate.Update(dto.HourlyLimit, dto.DailyLimit, dto.Enabled);
        await _unitOfWork.SaveChangesAsync(ct);

        // 更新缓存
        var updatedDto = ToDto(aggregate);
        Cache[channel] = updatedDto;

        // 审计日志
        _logger.LogWarning("AUDIT: 操作员 {OperatorId} 更新了渠道 {Channel} 的频率限制：HourlyLimit={HourlyLimit} DailyLimit={DailyLimit} Enabled={Enabled}",
            operatorId, channel, dto.HourlyLimit, dto.DailyLimit, dto.Enabled);
    }

    private static RateLimitConfigDto ToDto(NotificationRateLimitConfig aggregate)
        => new()
        {
            Channel = aggregate.Channel,
            HourlyLimit = aggregate.HourlyLimit,
            DailyLimit = aggregate.DailyLimit,
            Enabled = aggregate.Enabled
        };

    private static RateLimitConfigDto CloneDto(RateLimitConfigDto dto)
        => new()
        {
            Channel = dto.Channel,
            HourlyLimit = dto.HourlyLimit,
            DailyLimit = dto.DailyLimit,
            Enabled = dto.Enabled
        };
}
