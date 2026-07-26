using Leno.UserCenter.Application.DTOs;
using Leno.UserCenter.Application.Exceptions;
using Leno.UserCenter.Domain.Aggregates;
using Leno.UserCenter.Domain.Exceptions;
using Leno.UserCenter.Domain.Repositories;
using Leno.UserCenter.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.UserCenter.Application.Services;

/// <summary>
/// 通知偏好应用服务实现，编排查询与更新用户通知偏好的用例。
/// 首次查询时懒初始化默认偏好并持久化；更新时支持单事件单渠道与批量矩阵两种模式。
/// 站内信渠道始终强制开启，免打扰字段独立处理。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class NotificationPreferencesAppService : INotificationPreferencesAppService
{
    private readonly INotificationPreferencesRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationPreferencesAppService(
        INotificationPreferencesRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<NotificationPreferencesDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var preferences = await _repository.GetByUserIdAsync(userId, ct);
        if (preferences is null)
        {
            // 懒初始化：首次访问时按默认偏好创建并持久化
            preferences = NotificationPreferences.Create(Guid.NewGuid(), userId);
            await _repository.AddAsync(preferences, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }

        return ToDto(preferences);
    }

    /// <inheritdoc />
    public async Task<NotificationPreferencesDto> UpdateAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var preferences = await _repository.GetByUserIdAsync(userId, ct);
        if (preferences is null)
        {
            preferences = NotificationPreferences.Create(Guid.NewGuid(), userId);
            await _repository.AddAsync(preferences, ct);
        }

        // 1) 批量矩阵模式：全量替换偏好
        if (request.BatchSettings is { Count: > 0 })
        {
            var settings = request.BatchSettings
                .Select(s => (s.EventType, s.Channel, s.Enabled))
                .ToList();
            preferences.ReplaceAll(settings);
        }
        else if (request.EventType.HasValue && request.Channel.HasValue && request.Enabled.HasValue)
        {
            // 2) 单事件单渠道模式
            preferences.UpdateChannel(request.EventType.Value, request.Channel.Value, request.Enabled.Value);
        }
        else if (request.EventType.HasValue || request.Channel.HasValue || request.Enabled.HasValue)
        {
            // 部分字段缺失，按校验失败处理
            throw new UserCenterValidationException(
                "单事件单渠道更新模式需同时提供 eventType、channel 与 enabled 字段");
        }

        // 3) 免打扰字段独立处理
        if (request.DndEnabled.HasValue)
        {
            var (start, end) = ParseDndTimes(request.DndEnabled.Value, request.DndStart, request.DndEnd);
            preferences.UpdateDnd(request.DndEnabled.Value, start, end);
        }

        await _repository.UpdateAsync(preferences, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToDto(preferences);
    }

    private static (TimeSpan? start, TimeSpan? end) ParseDndTimes(bool enabled, string? startText, string? endText)
    {
        if (!enabled)
        {
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(startText) || string.IsNullOrWhiteSpace(endText))
        {
            throw new UserCenterDomainException(
                "启用免打扰时必须同时提供起止时间", "NOTIFICATION_DND_TIME_REQUIRED");
        }

        if (!TryParseTime(startText, out var start))
        {
            throw new UserCenterValidationException("免打扰起始时间格式无效，应为 HH:mm");
        }

        if (!TryParseTime(endText, out var end))
        {
            throw new UserCenterValidationException("免打扰结束时间格式无效，应为 HH:mm");
        }

        return (start, end);
    }

    private static bool TryParseTime(string text, out TimeSpan value)
    {
        return TimeSpan.TryParseExact(text, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static NotificationPreferencesDto ToDto(NotificationPreferences preferences)
    {
        return new NotificationPreferencesDto
        {
            UserId = preferences.UserId,
            Preferences = preferences.Items
                .Select(item => new NotificationPreferenceItemDto
                {
                    EventType = item.EventType,
                    Group = GetEventGroup(item.EventType),
                    DisplayName = GetEventDisplayName(item.EventType),
                    Channels = new NotificationChannelsDto
                    {
                        InApp = item.InAppEnabled,
                        Sms = item.SmsEnabled,
                        Email = item.EmailEnabled
                    }
                })
                .OrderBy(i => (int)i.EventType)
                .ToList(),
            DndEnabled = preferences.DndEnabled,
            DndStart = preferences.DndStart?.ToString(@"hh\:mm"),
            DndEnd = preferences.DndEnd?.ToString(@"hh\:mm"),
            UpdatedAt = preferences.UpdatedAt
        };
    }

    private static string GetEventGroup(NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.OrderStatus or NotificationEventType.LogisticsUpdate => "订单通知",
        NotificationEventType.CouponArrival or NotificationEventType.SeckillReminder => "促销通知",
        NotificationEventType.PointsEarned or NotificationEventType.PointsExpiring => "积分通知",
        NotificationEventType.SystemNotice => "系统通知",
        _ => "其他"
    };

    private static string GetEventDisplayName(NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.OrderStatus => "订单状态变更",
        NotificationEventType.LogisticsUpdate => "物流更新",
        NotificationEventType.CouponArrival => "优惠券到账",
        NotificationEventType.SeckillReminder => "秒杀提醒",
        NotificationEventType.PointsEarned => "积分到账",
        NotificationEventType.PointsExpiring => "积分过期提醒",
        NotificationEventType.SystemNotice => "系统通知",
        _ => eventType.ToString()
    };
}
