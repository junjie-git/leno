using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Aggregates;

/// <summary>
/// 通知模板聚合根，配置驱动通知标题与内容的变量插值。
/// 模板变量使用 {{variable}} 占位符语法，由 <c>ITemplateRenderer</c> 渲染。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>TemplateId</c>。
/// </summary>
public sealed class NotificationTemplate : AggregateRoot
{
    /// <summary>事件类型名（如 OrderCreatedEvent），同一事件类型每渠道仅一个启用模板。</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>通知渠道。</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>标题模板（含 {{variable}} 占位符）。</summary>
    public string TitleTemplate { get; private set; } = string.Empty;

    /// <summary>内容模板（含 {{variable}} 占位符）。</summary>
    public string ContentTemplate { get; private set; } = string.Empty;

    /// <summary>
    /// 模板变量名列表，持久化为 JSON。
    /// </summary>
    private List<string> _variables = [];
    public List<string> Variables { get => _variables; private set => _variables = value ?? []; }

    /// <summary>模板状态。</summary>
    public TemplateStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private NotificationTemplate() { }

    private NotificationTemplate(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建启用态模板。
    /// </summary>
    public static NotificationTemplate Create(
        Guid templateId,
        string eventType,
        NotificationChannel channel,
        string titleTemplate,
        string contentTemplate,
        List<string> variables)
    {
        ValidateCommon(templateId, eventType, channel, titleTemplate, contentTemplate);

        return new NotificationTemplate(templateId)
        {
            EventType = eventType,
            Channel = channel,
            TitleTemplate = titleTemplate,
            ContentTemplate = contentTemplate,
            Variables = variables ?? [],
            Status = TemplateStatus.Enabled
        };
    }

    /// <summary>
    /// 更新模板内容与变量。
    /// </summary>
    public void Update(string titleTemplate, string contentTemplate, List<string> variables)
    {
        ValidateCommon(Id, EventType, Channel, titleTemplate, contentTemplate);
        TitleTemplate = titleTemplate;
        ContentTemplate = contentTemplate;
        Variables = variables ?? [];
    }

    /// <summary>启用模板。</summary>
    public void Enable()
    {
        Status = TemplateStatus.Enabled;
    }

    /// <summary>禁用模板。</summary>
    public void Disable()
    {
        Status = TemplateStatus.Disabled;
    }

    private static void ValidateCommon(
        Guid templateId,
        string eventType,
        NotificationChannel channel,
        string titleTemplate,
        string contentTemplate)
    {
        if (templateId == Guid.Empty)
        {
            throw new NotificationDomainException("TemplateId 不可为空", "NOTIFICATION_TEMPLATE_ID_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new NotificationDomainException("EventType 不可为空", "NOTIFICATION_TEMPLATE_EVENT_TYPE_EMPTY");
        }

        if (!Enum.IsDefined(channel))
        {
            throw new NotificationDomainException($"通知渠道非法：{channel}", "NOTIFICATION_TEMPLATE_CHANNEL_INVALID");
        }

        if (string.IsNullOrWhiteSpace(titleTemplate))
        {
            throw new NotificationDomainException("标题模板不可为空", "NOTIFICATION_TEMPLATE_TITLE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(contentTemplate))
        {
            throw new NotificationDomainException("内容模板不可为空", "NOTIFICATION_TEMPLATE_CONTENT_EMPTY");
        }

        if (titleTemplate.Length > 200)
        {
            throw new NotificationDomainException("标题模板不可超过 200 字", "NOTIFICATION_TEMPLATE_TITLE_TOO_LONG");
        }

        if (contentTemplate.Length > 2000)
        {
            throw new NotificationDomainException("内容模板不可超过 2000 字", "NOTIFICATION_TEMPLATE_CONTENT_TOO_LONG");
        }
    }
}
