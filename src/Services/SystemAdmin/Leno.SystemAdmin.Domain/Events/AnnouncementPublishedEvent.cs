using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Events;

/// <summary>
/// 公告发布领域事件，系统管理域在 SystemAnnouncement.Publish 时由聚合根收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.AnnouncementPublishedIntegrationEvent"/> 集成事件对外发布。
/// </summary>
public sealed class AnnouncementPublishedEvent : DomainEventBase
{
    /// <summary>公告标识。</summary>
    public Guid AnnouncementId { get; init; }

    /// <summary>公告标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>公告类型（0=系统，1=维护，2=促销），以 int 传递避免跨域枚举依赖。</summary>
    public int Type { get; init; }

    public AnnouncementPublishedEvent(Guid announcementId, string title, int type)
        : base(announcementId)
    {
        AnnouncementId = announcementId;
        Title = title ?? string.Empty;
        Type = type;
    }
}
