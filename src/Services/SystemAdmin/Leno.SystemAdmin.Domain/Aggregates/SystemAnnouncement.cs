using Leno.SystemAdmin.Domain.Events;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 系统公告聚合根，封装公告生命周期与发布不变量。
/// 状态流转：Draft → Published → Expired；Published → Draft（撤回）。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>AnnouncementId</c>。
/// 发布时附加 <see cref="AnnouncementPublishedEvent"/>，驱动消息通知域推送。
/// </summary>
public sealed class SystemAnnouncement : AggregateRoot
{
    private const int MaxTitleLength = 200;
    private const int MaxContentLength = 4000;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid AnnouncementId => Id;

    /// <summary>公告标题，≤200 字。</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>公告正文，≤4000 字。</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>公告类型。</summary>
    public AnnouncementType Type { get; private set; }

    /// <summary>目标受众。</summary>
    public AnnouncementTargetAudience TargetAudience { get; private set; }

    /// <summary>计划发布时间（UTC），可空表示立即发布。</summary>
    public DateTime? PublishAt { get; private set; }

    /// <summary>过期时间（UTC），可空表示不过期。</summary>
    public DateTime? ExpireAt { get; private set; }

    /// <summary>公告状态。</summary>
    public AnnouncementStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private SystemAnnouncement() { }

    private SystemAnnouncement(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验标题/正文/类型/受众，计划发布时间若提供且早于当前时间则抛异常，初始状态为 Draft。
    /// </summary>
    /// <param name="announcementId">公告标识，由应用层生成。</param>
    /// <param name="title">公告标题。</param>
    /// <param name="content">公告正文。</param>
    /// <param name="announcementType">公告类型。</param>
    /// <param name="targetAudience">目标受众。</param>
    /// <param name="publishAt">计划发布时间（UTC），可空。</param>
    /// <param name="expireAt">过期时间（UTC），可空。</param>
    public static SystemAnnouncement Create(
        Guid announcementId,
        string title,
        string content,
        AnnouncementType announcementType,
        AnnouncementTargetAudience targetAudience,
        DateTime? publishAt,
        DateTime? expireAt)
    {
        if (announcementId == Guid.Empty)
        {
            throw new SystemAdminDomainException("公告标识不可为空", "ANNOUNCEMENT_ID_EMPTY");
        }

        ValidateTitle(title);
        ValidateContent(content);
        ValidateType(announcementType);
        ValidateTargetAudience(targetAudience);
        ValidatePublishAt(publishAt);

        return new SystemAnnouncement(announcementId)
        {
            Title = title.Trim(),
            Content = content,
            Type = announcementType,
            TargetAudience = targetAudience,
            PublishAt = publishAt,
            ExpireAt = expireAt,
            Status = AnnouncementStatus.Draft
        };
    }

    /// <summary>
    /// 发布公告，仅 Draft 态可发布；计划发布时间为空时置为当前时间；附加 <see cref="AnnouncementPublishedEvent"/>。
    /// </summary>
    public void Publish()
    {
        if (Status == AnnouncementStatus.Published)
        {
            throw new SystemAdminDomainException("公告已发布，不可重复发布", "ANNOUNCEMENT_ALREADY_PUBLISHED");
        }

        if (Status == AnnouncementStatus.Expired)
        {
            throw new SystemAdminDomainException("公告已过期，不可发布", "ANNOUNCEMENT_EXPIRED");
        }

        Status = AnnouncementStatus.Published;
        PublishAt ??= DateTime.UtcNow;

        AddDomainEvent(new AnnouncementPublishedEvent(Id, Title, (int)Type));
    }

    /// <summary>
    /// 撤回公告，仅 Published 态可撤回至 Draft。
    /// </summary>
    public void Unpublish()
    {
        if (Status != AnnouncementStatus.Published)
        {
            throw new SystemAdminDomainException("仅已发布公告可撤回", "ANNOUNCEMENT_NOT_PUBLISHED");
        }

        Status = AnnouncementStatus.Draft;
    }

    /// <summary>
    /// 更新公告内容，仅 Draft 态可更新。
    /// </summary>
    /// <param name="title">公告标题。</param>
    /// <param name="content">公告正文。</param>
    /// <param name="announcementType">公告类型。</param>
    /// <param name="targetAudience">目标受众。</param>
    /// <param name="publishAt">计划发布时间（UTC），可空。</param>
    /// <param name="expireAt">过期时间（UTC），可空。</param>
    public void Update(
        string title,
        string content,
        AnnouncementType announcementType,
        AnnouncementTargetAudience targetAudience,
        DateTime? publishAt,
        DateTime? expireAt)
    {
        if (Status != AnnouncementStatus.Draft)
        {
            throw new SystemAdminDomainException($"当前状态为 {Status}，仅草稿态可更新", "ANNOUNCEMENT_NOT_DRAFT");
        }

        ValidateTitle(title);
        ValidateContent(content);
        ValidateType(announcementType);
        ValidateTargetAudience(targetAudience);
        ValidatePublishAt(publishAt);

        Title = title.Trim();
        Content = content;
        Type = announcementType;
        TargetAudience = targetAudience;
        PublishAt = publishAt;
        ExpireAt = expireAt;
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new SystemAdminDomainException("公告标题不可为空", "ANNOUNCEMENT_TITLE_EMPTY");
        }

        if (title.Trim().Length > MaxTitleLength)
        {
            throw new SystemAdminDomainException($"公告标题长度不可超过 {MaxTitleLength} 字符", "ANNOUNCEMENT_TITLE_LENGTH");
        }
    }

    private static void ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new SystemAdminDomainException("公告正文不可为空", "ANNOUNCEMENT_CONTENT_EMPTY");
        }

        if (content.Length > MaxContentLength)
        {
            throw new SystemAdminDomainException($"公告正文长度不可超过 {MaxContentLength} 字符", "ANNOUNCEMENT_CONTENT_LENGTH");
        }
    }

    private static void ValidateType(AnnouncementType announcementType)
    {
        if (!Enum.IsDefined(announcementType))
        {
            throw new SystemAdminDomainException("公告类型取值非法", "ANNOUNCEMENT_TYPE_INVALID");
        }
    }

    private static void ValidateTargetAudience(AnnouncementTargetAudience targetAudience)
    {
        if (!Enum.IsDefined(targetAudience))
        {
            throw new SystemAdminDomainException("目标受众取值非法", "ANNOUNCEMENT_AUDIENCE_INVALID");
        }
    }

    private static void ValidatePublishAt(DateTime? publishAt)
    {
        if (publishAt.HasValue && publishAt.Value < DateTime.UtcNow)
        {
            throw new SystemAdminDomainException("计划发布时间不可早于当前时间", "ANNOUNCEMENT_PUBLISH_AT_PAST");
        }
    }
}
