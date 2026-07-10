using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Services;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 模板渲染器实现，支持 {{variable}} 占位符语法。
/// 变量名不区分大小写，未提供的变量保留原占位符。
/// </summary>
public sealed class TemplateRenderer : ITemplateRenderer
{
    /// <inheritdoc />
    public (string Title, string Content) Render(NotificationTemplate notificationTemplate, Dictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(notificationTemplate);
        ArgumentNullException.ThrowIfNull(variables);

        var title = RenderTemplate(notificationTemplate.TitleTemplate, variables);
        var content = RenderTemplate(notificationTemplate.ContentTemplate, variables);
        return (title, content);
    }

    private static string RenderTemplate(string templateText, Dictionary<string, string> variables)
    {
        var result = templateText;
        foreach (var kv in variables)
        {
            if (string.IsNullOrEmpty(kv.Key))
            {
                continue;
            }

            result = result.Replace("{{" + kv.Key + "}}", kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
