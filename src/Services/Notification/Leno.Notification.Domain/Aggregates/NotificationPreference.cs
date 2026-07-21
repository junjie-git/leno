using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Aggregates;

/// <summary>
/// 用户通知偏好聚合根，按事件类型配置通知渠道。
/// 未配置偏好时使用默认渠道（站内信）。
/// 聚合标识 <see cref="Entity.Id"/> 即 <c>PreferenceId</c>，与 UserId 一对一。
/// </summary>
public sealed class NotificationPreference : AggregateRoot
{
    /// <summary>
    /// 默认渠道列表（站内信），未配置偏好时复用此引用，避免每次调用 GetChannels 分配新 List。
    /// 该列表为不可变快照，调用方不应修改；EventChannels 中存储的用户自定义列表仍为独立实例。
    /// </summary>
    private static readonly List<NotificationChannel> DefaultInAppChannels = [NotificationChannel.InApp];

    /// <summary>用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// 事件类型 → 渠道列表 的偏好字典，持久化为 JSON。
    /// </summary>
    private Dictionary<string, List<NotificationChannel>> _eventChannels = [];
    public Dictionary<string, List<NotificationChannel>> EventChannels { get => _eventChannels; private set => _eventChannels = value ?? []; }

    /// <summary>偏好状态。</summary>
    public PreferenceStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private NotificationPreference() { }

    private NotificationPreference(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建启用态空偏好。
    /// </summary>
    public static NotificationPreference Create(Guid preferenceId, Guid userId)
    {
        if (preferenceId == Guid.Empty)
        {
            throw new NotificationDomainException("PreferenceId 不可为空", "NOTIFICATION_PREFERENCE_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new NotificationDomainException("UserId 不可为空", "NOTIFICATION_PREFERENCE_USER_EMPTY");
        }

        return new NotificationPreference(preferenceId)
        {
            UserId = userId,
            EventChannels = [],
            Status = PreferenceStatus.Active
        };
    }

    /// <summary>
    /// 设置某事件类型的通知渠道偏好。
    /// </summary>
    public void SetChannelPreference(string eventType, List<NotificationChannel> channels)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new NotificationDomainException("EventType 不可为空", "NOTIFICATION_PREFERENCE_EVENT_TYPE_EMPTY");
        }

        if (channels is null || channels.Count == 0)
        {
            _eventChannels.Remove(eventType);
            return;
        }

        foreach (var c in channels)
        {
            if (!Enum.IsDefined(c))
            {
                throw new NotificationDomainException($"通知渠道非法：{c}", "NOTIFICATION_PREFERENCE_CHANNEL_INVALID");
            }
        }

        _eventChannels[eventType] = channels;
    }

    /// <summary>
    /// 获取某事件类型的渠道偏好，未配置则返回默认（站内信）。
    /// </summary>
    public List<NotificationChannel> GetChannels(string eventType)
    {
        if (_eventChannels.TryGetValue(eventType, out var channels) && channels.Count > 0)
        {
            return channels;
        }

        // P2-45：返回缓存的默认列表引用，避免每次调用分配新 List<NotificationChannel>。
        return DefaultInAppChannels;
    }

    /// <summary>启用通知。</summary>
    public void Enable()
    {
        Status = PreferenceStatus.Active;
    }

    /// <summary>禁用所有通知。</summary>
    public void Disable()
    {
        Status = PreferenceStatus.Inactive;
    }
}
