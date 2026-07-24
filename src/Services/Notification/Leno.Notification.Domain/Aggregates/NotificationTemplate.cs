using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Aggregates;

/// <summary>
/// 通知模板聚合根，配置驱动通知标题与内容的变量插值。
/// 模板变量使用 {{variable}} 占位符语法，由 <c>ITemplateRenderer</c> 渲染。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>TemplateId</c>。
/// </summary>
public sealed class NotificationTemplate : AggregateRoot, ITenantEntity
{
    /// <summary>模板编码（如 OrderCreated），同一编码每渠道仅一个启用模板。</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>模板名称（用户友好名称）。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>通知渠道。</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>标题/主题模板（含 {{variable}} 占位符）。</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>内容/正文模板（含 {{variable}} 占位符）。</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>短信模板编码（仅 Sms 渠道使用，可选）。</summary>
    public string? SmsTemplateCode { get; private set; }

    /// <summary>模板描述。</summary>
    public string? Description { get; private set; }

    /// <summary>操作人标识（运营端创建/编辑时记录）。</summary>
    public Guid? OperatorId { get; private set; }

    /// <summary>
    /// 租户 ID（多租户预留扩展位，4.7）。
    /// <para>
    /// 当前阶段默认 <c>null</c>（单租户模式 / 全局数据），由 <c>BaseDbContext</c> 全局查询过滤器保证默认行为不变。
    /// DG-7 决策门通过后，由 <c>TenantQueryFilterInterceptor</c> 在保存时自动填充当前租户 ID。
    /// </para>
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// 模板文化维度（国际化预留扩展位，DG-8 决策门通过后实际启用）。
    /// <para>
    /// 当前阶段默认 <c>null</c>，语义等同 <see cref="NotificationTemplateCulture.Default"/>（zh-CN），
    /// 保证现有模板行为零变更。业务方确认海外扩展计划后，按 <c>TemplateCode + Culture</c> 维度
    /// 创建多语言变体模板。
    /// </para>
    /// </summary>
    public NotificationTemplateCulture? Culture { get; private set; }

    /// <summary>
    /// 模板生效文化（<see cref="Culture"/> 为 <c>null</c> 时回退到 <see cref="NotificationTemplateCulture.Default"/>）。
    /// 供模板渲染与查询时统一获取文化维度，避免调用方重复处理 null 回退逻辑。
    /// </summary>
    public NotificationTemplateCulture EffectiveCulture => Culture ?? NotificationTemplateCulture.Default;

    /// <summary>
    /// 模板变量列表，持久化为 JSON。
    /// </summary>
    private List<TemplateVariable> _variables = [];
    public List<TemplateVariable> Variables { get => _variables; private set => _variables = value ?? []; }

    /// <summary>模板状态。</summary>
    public TemplateStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private NotificationTemplate() { }

    private NotificationTemplate(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建启用态模板。
    /// </summary>
    /// <param name="culture">
    /// 模板文化维度（国际化预留扩展位）。为 <c>null</c> 时语义等同 zh-CN（默认行为不变），
    /// DG-8 决策门通过后传入具体 <see cref="NotificationTemplateCulture"/> 创建多语言变体。
    /// </param>
    public static NotificationTemplate Create(
        Guid templateId,
        string code,
        string name,
        NotificationChannel channel,
        string subject,
        string body,
        List<TemplateVariable> variables,
        string? smsTemplateCode = null,
        string? description = null,
        Guid? operatorId = null,
        NotificationTemplateCulture? culture = null)
    {
        ValidateCommon(templateId, code, name, channel, subject, body);

        return new NotificationTemplate(templateId)
        {
            Code = code,
            Name = name,
            Channel = channel,
            Subject = subject,
            Body = body,
            Variables = variables ?? [],
            SmsTemplateCode = smsTemplateCode,
            Description = description,
            OperatorId = operatorId,
            Culture = culture,
            Status = TemplateStatus.Enabled
        };
    }

    /// <summary>
    /// 更新模板文化维度（国际化预留扩展位）。
    /// <para>
    /// 当前阶段调用方通常不调用此方法（Culture 保持 <c>null</c> = zh-CN 默认行为）。
    /// DG-8 决策门通过后，运营端按 <c>TemplateCode + Culture</c> 维度管理多语言变体时调用。
    /// </para>
    /// </summary>
    /// <param name="culture">目标文化，为 <c>null</c> 时回退到默认 zh-CN 语义。</param>
    public void UpdateCulture(NotificationTemplateCulture? culture)
    {
        Culture = culture;
    }

    /// <summary>
    /// 更新模板内容与变量。
    /// </summary>
    /// <param name="subject">标题模板。</param>
    /// <param name="body">内容模板。</param>
    /// <param name="variables">模板变量列表。</param>
    /// <param name="smsTemplateCode">
    /// 短信模板编码，<c>null</c> 表示不修改既有值；非空时按渠道格式校验：
    /// 阿里云形如 <c>SMS_12345678</c>（前缀 <c>SMS_</c>），腾讯云为纯数字。
    /// </param>
    public void Update(
        string subject,
        string body,
        List<TemplateVariable> variables,
        string? smsTemplateCode = null)
    {
        ValidateCommon(Id, Code, Name, Channel, subject, body);

        // P2-46：smsTemplateCode=null 表示不修改；非 null 时校验格式（SMS_ 前缀或纯数字），避免脏值落库后渠道发送失败。
        if (smsTemplateCode is not null)
        {
            if (!IsValidSmsTemplateCode(smsTemplateCode))
            {
                throw new NotificationDomainException(
                    $"短信模板编码格式非法：{smsTemplateCode}（需 SMS_ 前缀或纯数字）",
                    "NOTIFICATION_TEMPLATE_SMS_CODE_INVALID");
            }

            SmsTemplateCode = smsTemplateCode;
        }

        Subject = subject;
        Body = body;
        Variables = variables ?? [];
    }

    /// <summary>
    /// 校验短信模板编码格式：阿里云 SMS_ 前缀，或腾讯云纯数字；空字符串视为非法。
    /// </summary>
    private static bool IsValidSmsTemplateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        if (code.StartsWith("SMS_", StringComparison.Ordinal))
        {
            // 阿里云 SMS_ 后须至少一位非空白字符
            return code.Length > "SMS_".Length;
        }

        // 腾讯云纯数字模板 ID
        return code.All(char.IsDigit);
    }

    /// <summary>
    /// 添加模板变量。
    /// </summary>
    public void AddVariable(TemplateVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);

        if (_variables.Any(v => v.Name.Equals(variable.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotificationDomainException($"变量 {variable.Name} 已存在", "NOTIFICATION_VARIABLE_DUPLICATE");
        }

        _variables.Add(variable);
    }

    /// <summary>
    /// 移除模板变量。
    /// </summary>
    public void RemoveVariable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new NotificationDomainException("变量名不可为空", "NOTIFICATION_VARIABLE_NAME_EMPTY");
        }

        var existing = _variables.FirstOrDefault(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _variables.Remove(existing);
        }
    }

    /// <summary>
    /// 判断模板是否包含指定占位符变量。
    /// </summary>
    public bool ContainsPlaceholder(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var placeholder = $"{{{{{name}}}}}";
        return Subject.Contains(placeholder, StringComparison.OrdinalIgnoreCase)
               || Body.Contains(placeholder, StringComparison.OrdinalIgnoreCase);
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
        string code,
        string name,
        NotificationChannel channel,
        string subject,
        string body)
    {
        if (templateId == Guid.Empty)
        {
            throw new NotificationDomainException("TemplateId 不可为空", "NOTIFICATION_TEMPLATE_ID_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new NotificationDomainException("Code 不可为空", "NOTIFICATION_TEMPLATE_CODE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new NotificationDomainException("Name 不可为空", "NOTIFICATION_TEMPLATE_NAME_EMPTY");
        }

        if (!Enum.IsDefined(channel))
        {
            throw new NotificationDomainException($"通知渠道非法：{channel}", "NOTIFICATION_TEMPLATE_CHANNEL_INVALID");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new NotificationDomainException("标题模板不可为空", "NOTIFICATION_TEMPLATE_SUBJECT_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new NotificationDomainException("内容模板不可为空", "NOTIFICATION_TEMPLATE_BODY_EMPTY");
        }

        if (subject.Length > 200)
        {
            throw new NotificationDomainException("标题模板不可超过 200 字", "NOTIFICATION_TEMPLATE_SUBJECT_TOO_LONG");
        }

        if (body.Length > 2000)
        {
            throw new NotificationDomainException("内容模板不可超过 2000 字", "NOTIFICATION_TEMPLATE_BODY_TOO_LONG");
        }

        if (code.Length > 128)
        {
            throw new NotificationDomainException("Code 不可超过 128 字", "NOTIFICATION_TEMPLATE_CODE_TOO_LONG");
        }

        if (name.Length > 128)
        {
            throw new NotificationDomainException("Name 不可超过 128 字", "NOTIFICATION_TEMPLATE_NAME_TOO_LONG");
        }
    }
}