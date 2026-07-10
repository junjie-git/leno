using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Events;

/// <summary>
/// 公告发布集成事件，系统管理域在 SystemAnnouncement.Publish 时发布。
/// 消费方：消息通知域（向目标受众推送公告通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class AnnouncementPublishedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>公告标识。</summary>
    public Guid AnnouncementId { get; init; }

    /// <summary>公告标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>公告类型（0=系统，1=维护，2=促销），以 int 传递避免跨域枚举依赖。</summary>
    public int Type { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AnnouncementId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public AnnouncementPublishedEvent() : base()
    {
    }

    public AnnouncementPublishedEvent(Guid announcementId, string title, int type) : base()
    {
        AnnouncementId = announcementId;
        Title = title ?? string.Empty;
        Type = type;
    }
}
