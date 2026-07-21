using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Aggregates;

/// <summary>
/// 通知渠道频率限制配置聚合根，按渠道持久化限流阈值。
/// 聚合标识 <see cref="Entity.Id"/> 由 Channel 派生（一对一）。
/// </summary>
public sealed class NotificationRateLimitConfig : AggregateRoot
{
    /// <summary>通知渠道（业务唯一键）。</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>每小时限制条数。</summary>
    public int HourlyLimit { get; private set; }

    /// <summary>每日限制条数（仅 SMS 渠道）。</summary>
    public int? DailyLimit { get; private set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private NotificationRateLimitConfig() { }

    private NotificationRateLimitConfig(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建限流配置。
    /// </summary>
    public static NotificationRateLimitConfig Create(
        Guid id,
        NotificationChannel channel,
        int hourlyLimit,
        int? dailyLimit,
        bool enabled)
    {
        if (id == Guid.Empty)
        {
            throw new NotificationDomainException("Id 不可为空", "NOTIFICATION_RATE_LIMIT_ID_EMPTY");
        }

        if (!Enum.IsDefined(channel))
        {
            throw new NotificationDomainException($"通知渠道非法：{channel}", "NOTIFICATION_RATE_LIMIT_CHANNEL_INVALID");
        }

        if (hourlyLimit < 0)
        {
            throw new NotificationDomainException("每小时限制条数不可为负", "NOTIFICATION_RATE_LIMIT_HOURLY_INVALID");
        }

        if (dailyLimit.HasValue && dailyLimit.Value < 0)
        {
            throw new NotificationDomainException("每日限制条数不可为负", "NOTIFICATION_RATE_LIMIT_DAILY_INVALID");
        }

        return new NotificationRateLimitConfig(id)
        {
            Channel = channel,
            HourlyLimit = hourlyLimit,
            DailyLimit = dailyLimit,
            Enabled = enabled
        };
    }

    /// <summary>
    /// 更新限流阈值。
    /// </summary>
    public void Update(int? hourlyLimit, int? dailyLimit, bool? enabled)
    {
        if (hourlyLimit.HasValue)
        {
            if (hourlyLimit.Value < 0)
            {
                throw new NotificationDomainException("每小时限制条数不可为负", "NOTIFICATION_RATE_LIMIT_HOURLY_INVALID");
            }
            HourlyLimit = hourlyLimit.Value;
        }

        if (dailyLimit.HasValue)
        {
            if (dailyLimit.Value < 0)
            {
                throw new NotificationDomainException("每日限制条数不可为负", "NOTIFICATION_RATE_LIMIT_DAILY_INVALID");
            }
            DailyLimit = dailyLimit.Value;
        }

        if (enabled.HasValue)
        {
            Enabled = enabled.Value;
        }
    }
}
