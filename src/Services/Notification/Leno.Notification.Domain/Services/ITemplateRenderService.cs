using Leno.Notification.Domain.Aggregates;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 模板渲染服务接口，提供异步渲染方法，支持变量校验、HTML 转义和渲染快照。
/// 由基础设施层实现。
/// </summary>
public interface ITemplateRenderService
{
    /// <summary>
    /// 异步渲染模板，返回标题与内容的渲染结果。
    /// </summary>
    /// <param name="notificationTemplate">通知模板聚合。</param>
    /// <param name="variables">变量键值对，键为变量名（不含花括号），值为替换内容。</param>
    /// <returns>渲染结果，包含标题、内容和渲染快照。</returns>
    /// <exception cref="Leno.Notification.Domain.Exceptions.NotificationDomainException">
    /// 必填变量缺失时抛出。
    /// </exception>
    Task<TemplateRenderResult> RenderAsync(NotificationTemplate notificationTemplate, Dictionary<string, string> variables);

    /// <summary>
    /// 验证模板 Body 中是否包含未定义的占位符（即模板中有 {{xxx}} 但未在 Variables 中声明）。
    /// 用于保存模板时校验。
    /// </summary>
    /// <param name="notificationTemplate">通知模板。</param>
    /// <returns>未定义的占位符列表，为空表示所有占位符均已在变量中声明。</returns>
    List<string> ValidateUndefinedPlaceholders(NotificationTemplate notificationTemplate);
}

/// <summary>
/// 模板渲染结果。
/// </summary>
public sealed class TemplateRenderResult
{
    /// <summary>渲染后的标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>渲染后的内容。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>渲染快照（JSON），用于冻结到 NotificationRecord.ContentSnapshot。</summary>
    public string ContentSnapshot { get; set; } = string.Empty;
}