using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Aggregates;

/// <summary>
/// 通知偏好聚合根，封装用户对各类通知事件与渠道的开关矩阵，以及免打扰时段设置。
/// 同一用户仅有一条 <see cref="NotificationPreferences"/> 记录，首次查询时由应用层懒初始化为默认偏好。
/// 站内信（InApp）渠道默认开启且不可关闭，保证关键通知触达（INV-NP-01）。
/// </summary>
public sealed class NotificationPreferences : AggregateRoot
{
    private readonly List<NotificationPreferenceItem> _items = new();

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>事件-渠道偏好项集合，每项对应一个事件类型的 3 个渠道开关。</summary>
    public IReadOnlyCollection<NotificationPreferenceItem> Items => _items.AsReadOnly();

    /// <summary>是否启用免打扰时段。</summary>
    public bool DndEnabled { get; private set; }

    /// <summary>免打扰起始时间（本地时间，HH:mm），仅在 <see cref="DndEnabled"/> 为 true 时生效。</summary>
    public TimeSpan? DndStart { get; private set; }

    /// <summary>免打扰结束时间（本地时间，HH:mm），仅在 <see cref="DndEnabled"/> 为 true 时生效。</summary>
    public TimeSpan? DndEnd { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private NotificationPreferences() { }

    private NotificationPreferences(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建带有默认偏好（所有事件 InApp 开启、Sms/Email 关闭、免打扰关闭）的通知偏好聚合。
    /// </summary>
    public static NotificationPreferences Create(Guid id, Guid userId)
    {
        if (id == Guid.Empty)
        {
            throw new UserAuthDomainException("通知偏好标识不可为空", "NOTIFICATION_PREFERENCES_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new UserAuthDomainException("用户标识不可为空", "NOTIFICATION_PREFERENCES_USER_EMPTY");
        }

        var preferences = new NotificationPreferences(id)
        {
            UserId = userId,
            DndEnabled = false,
            DndStart = null,
            DndEnd = null
        };

        foreach (var eventType in Enum.GetValues<NotificationEventType>())
        {
            preferences._items.Add(NotificationPreferenceItem.CreateDefault(eventType));
        }

        return preferences;
    }

    /// <summary>
    /// 更新指定事件指定渠道的开关状态。
    /// 站内信（InApp）渠道禁止关闭（INV-NP-01）。
    /// 若事件不存在于偏好集合（不应发生），按需追加并应用默认值。
    /// </summary>
    public void UpdateChannel(NotificationEventType eventType, NotificationChannel channel, bool enabled)
    {
        if (channel == NotificationChannel.InApp && !enabled)
        {
            throw new UserAuthDomainException(
                "站内信渠道默认开启且不可关闭，保证关键通知触达", "NOTIFICATION_INAPP_CANNOT_DISABLE");
        }

        var item = _items.FirstOrDefault(i => i.EventType == eventType);
        if (item is null)
        {
            item = NotificationPreferenceItem.CreateDefault(eventType);
            _items.Add(item);
        }

        item.UpdateChannel(channel, enabled);
    }

    /// <summary>
    /// 批量替换全部偏好项。用于「保存设置」按钮一次性提交整张开关矩阵。
    /// 站内信渠道全部强制为开启状态（INV-NP-01）。
    /// </summary>
    public void ReplaceAll(IEnumerable<(NotificationEventType EventType, NotificationChannel Channel, bool Enabled)> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // 重置为默认（InApp 开启，Sms/Email 关闭）
        _items.Clear();
        foreach (var eventType in Enum.GetValues<NotificationEventType>())
        {
            _items.Add(NotificationPreferenceItem.CreateDefault(eventType));
        }

        foreach (var (eventType, channel, enabled) in settings)
        {
            // 站内信始终强制开启，忽略前端传入的 false
            var effectiveEnabled = channel == NotificationChannel.InApp ? true : enabled;
            UpdateChannel(eventType, channel, effectiveEnabled);
        }
    }

    /// <summary>
    /// 更新免打扰时段设置。<paramref name="start"/> 与 <paramref name="end"/> 必须同时提供或同时为空。
    /// 启用免打扰时两者均不可为空（INV-NP-02）。
    /// </summary>
    public void UpdateDnd(bool enabled, TimeSpan? start, TimeSpan? end)
    {
        if (enabled && (!start.HasValue || !end.HasValue))
        {
            throw new UserAuthDomainException(
                "启用免打扰时必须同时提供起止时间", "NOTIFICATION_DND_TIME_REQUIRED");
        }

        DndEnabled = enabled;
        DndStart = enabled ? start : null;
        DndEnd = enabled ? end : null;
    }
}

/// <summary>
/// 通知偏好项实体，作为 <see cref="NotificationPreferences"/> 聚合根的 owned collection。
/// 每项对应一个事件类型的 3 个渠道开关状态。
/// </summary>
public sealed class NotificationPreferenceItem
{
    /// <summary>事件类型。</summary>
    public NotificationEventType EventType { get; private set; }

    /// <summary>站内信渠道是否启用（默认开启且不可关闭）。</summary>
    public bool InAppEnabled { get; private set; }

    /// <summary>短信渠道是否启用。</summary>
    public bool SmsEnabled { get; private set; }

    /// <summary>邮件渠道是否启用。</summary>
    public bool EmailEnabled { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private NotificationPreferenceItem() { }

    internal NotificationPreferenceItem(NotificationEventType eventType, bool inApp, bool sms, bool email)
    {
        EventType = eventType;
        InAppEnabled = inApp;
        SmsEnabled = sms;
        EmailEnabled = email;
    }

    /// <summary>创建默认偏好项：InApp 开启，Sms/Email 关闭。</summary>
    internal static NotificationPreferenceItem CreateDefault(NotificationEventType eventType)
        => new(eventType, inApp: true, sms: false, email: false);

    /// <summary>
    /// 更新指定渠道开关。站内信渠道禁止关闭（INV-NP-01）。
    /// </summary>
    internal void UpdateChannel(NotificationChannel channel, bool enabled)
    {
        if (channel == NotificationChannel.InApp && !enabled)
        {
            throw new UserAuthDomainException(
                "站内信渠道默认开启且不可关闭", "NOTIFICATION_INAPP_CANNOT_DISABLE");
        }

        switch (channel)
        {
            case NotificationChannel.InApp:
                InAppEnabled = true; // 强制开启
                break;
            case NotificationChannel.Sms:
                SmsEnabled = enabled;
                break;
            case NotificationChannel.Email:
                EmailEnabled = enabled;
                break;
            default:
                throw new UserAuthDomainException(
                    $"不支持的通知渠道：{channel}", "NOTIFICATION_CHANNEL_INVALID");
        }
    }
}
