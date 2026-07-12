using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.Services;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 模板渲染器实现，支持 {{variable}} 占位符语法。
/// 特性：
/// - 必填变量缺失 → 抛出 NotificationDomainException 拒绝发送
/// - 可选变量缺失 → 渲染成功（保留原占位符或替换为空）
/// - HTML 特殊字符转义 → 防止 XSS 注入
/// - 渲染快照冻结 → 序列化为 JSON 保存到 ContentSnapshot
/// - 未定义占位符校验 → 保存模板时验证
/// </summary>
public sealed partial class TemplateRenderer : ITemplateRenderer, ITemplateRenderService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // 匹配 {{variable}} 占位符的正则表达式
    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    // HTML 特殊字符需要转义的内容
    private static readonly Dictionary<char, string> HtmlEscapes = new()
    {
        ['&'] = "&amp;",
        ['<'] = "&lt;",
        ['>'] = "&gt;",
        ['"'] = "&quot;",
        ['\''] = "&#39;"
    };

    /// <inheritdoc />
    public (string Title, string Content) Render(NotificationTemplate notificationTemplate, Dictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(notificationTemplate);
        ArgumentNullException.ThrowIfNull(variables);

        var title = RenderTemplate(notificationTemplate.Subject, variables, escapeHtml: false);
        var content = RenderTemplate(notificationTemplate.Body, variables, escapeHtml: true);
        return (title, content);
    }

    /// <inheritdoc />
    public Task<TemplateRenderResult> RenderAsync(NotificationTemplate notificationTemplate, Dictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(notificationTemplate);
        ArgumentNullException.ThrowIfNull(variables);

        // 1. 校验必填变量
        ValidateRequiredVariables(notificationTemplate, variables);

        // 2. 渲染标题（不转义 HTML，标题通常是纯文本）
        var title = RenderTemplate(notificationTemplate.Subject, variables, escapeHtml: false);

        // 3. 渲染内容（转义 HTML 特殊字符，防止注入）
        var content = RenderTemplate(notificationTemplate.Body, variables, escapeHtml: true);

        // 4. 冻结渲染快照，保存到 ContentSnapshot
        var snapshot = CreateSnapshot(notificationTemplate, variables, title, content);

        return Task.FromResult(new TemplateRenderResult
        {
            Title = title,
            Content = content,
            ContentSnapshot = snapshot
        });
    }

    /// <inheritdoc />
    public List<string> ValidateUndefinedPlaceholders(NotificationTemplate notificationTemplate)
    {
        ArgumentNullException.ThrowIfNull(notificationTemplate);

        var undefined = new List<string>();

        // 提取模板中所有的 {{variable}} 占位符
        var bodyPlaceholders = ExtractPlaceholders(notificationTemplate.Body);
        var subjectPlaceholders = ExtractPlaceholders(notificationTemplate.Subject);
        var allPlaceholders = new HashSet<string>(bodyPlaceholders.Concat(subjectPlaceholders), StringComparer.OrdinalIgnoreCase);

        // 已声明的变量名集合
        var declaredVariables = new HashSet<string>(
            notificationTemplate.Variables.Select(v => v.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var placeholder in allPlaceholders)
        {
            if (!declaredVariables.Contains(placeholder))
            {
                undefined.Add(placeholder);
            }
        }

        return undefined;
    }

    /// <summary>
    /// 校验必填变量是否存在。
    /// </summary>
    private static void ValidateRequiredVariables(NotificationTemplate template, Dictionary<string, string> variables)
    {
        var requiredVariables = template.Variables
            .Where(v => v.Required)
            .Select(v => v.Name)
            .ToList();

        foreach (var required in requiredVariables)
        {
            if (!variables.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new NotificationDomainException(
                    $"必填变量 {required} 缺失或为空",
                    "TEMPLATE_REQUIRED_VARIABLE_MISSING",
                    400);
            }
        }
    }

    /// <summary>
    /// 渲染模板文本，替换占位符并可选转义 HTML。
    /// </summary>
    private static string RenderTemplate(string templateText, Dictionary<string, string> variables, bool escapeHtml)
    {
        var result = PlaceholderRegex().Replace(templateText, match =>
        {
            var variableName = match.Groups[1].Value;
            if (variables.TryGetValue(variableName, out var value) && value is not null)
            {
                return escapeHtml ? HtmlEncode(value) : value;
            }

            // 可选变量缺失：保留原占位符
            return match.Value;
        });

        return result;
    }

    /// <summary>
    /// 从模板文本中提取所有 {{variable}} 占位符变量名。
    /// </summary>
    private static List<string> ExtractPlaceholders(string templateText)
    {
        var matches = PlaceholderRegex().Matches(templateText);
        var placeholders = new List<string>();
        foreach (Match match in matches)
        {
            placeholders.Add(match.Groups[1].Value);
        }
        return placeholders;
    }

    /// <summary>
    /// HTML 特殊字符转义，防止 XSS 注入。
    /// </summary>
    private static string HtmlEncode(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (HtmlEscapes.TryGetValue(c, out var escaped))
            {
                sb.Append(escaped);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 创建渲染快照，包含模板信息、变量和渲染结果。
    /// </summary>
    private static string CreateSnapshot(NotificationTemplate template, Dictionary<string, string> variables, string title, string content)
    {
        var snapshot = new
        {
            TemplateCode = template.Code,
            TemplateChannel = template.Channel.ToString(),
            RenderedAt = DateTime.UtcNow.ToString("O"),
            Variables = variables,
            Title = title,
            Content = content
        };

        return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
    }
}