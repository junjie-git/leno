using Leno.Notification.Domain.Aggregates;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 模板渲染器接口，将模板中的 {{variable}} 占位符替换为实际值。
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// 渲染模板，返回标题与内容元组。
    /// </summary>
    /// <param name="notificationTemplate">通知模板聚合。</param>
    /// <param name="variables">变量键值对，键为变量名（不含花括号），值为替换内容。</param>
    (string Title, string Content) Render(NotificationTemplate notificationTemplate, Dictionary<string, string> variables);
}
