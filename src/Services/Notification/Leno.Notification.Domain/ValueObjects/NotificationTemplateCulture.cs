using System.Globalization;
using Leno.Notification.Domain.Exceptions;

namespace Leno.Notification.Domain.ValueObjects;

/// <summary>
/// 通知模板文化值对象（国际化预留扩展位，DG-8 决策门通过后实际启用）。
/// <para>
/// 表示通知模板的语言文化维度（如 "zh-CN"、"en-US"）。
/// 当前阶段 <see cref="NotificationTemplate.Aggregates.NotificationTemplate.Culture"/> 默认为 <c>null</c>，
/// 语义等同 <see cref="Default"/>（zh-CN），保证现有模板行为零变更。
/// </para>
/// </summary>
public sealed class NotificationTemplateCulture : IEquatable<NotificationTemplateCulture>
{
    /// <summary>默认文化（zh-CN），当前阶段 null Culture 的等效语义。</summary>
    public static readonly NotificationTemplateCulture Default = new("zh-CN");

    private const int MaxCultureLength = 16;

    /// <summary>文化标识（如 "zh-CN"、"en-US"），遵循 BCP 47。</summary>
    public string Culture { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private NotificationTemplateCulture()
    {
        Culture = Default.Culture;
    }

    private NotificationTemplateCulture(string culture)
    {
        Culture = culture;
    }

    /// <summary>
    /// 工厂方法，创建文化值对象。
    /// </summary>
    /// <param name="culture">文化标识（如 "zh-CN"、"en-US"），须为有效的 <see cref="CultureInfo"/>。</param>
    /// <exception cref="NotificationDomainException">文化为空或非合法 BCP 47 标识时抛出。</exception>
    public static NotificationTemplateCulture Create(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            throw new NotificationDomainException(
                "通知模板文化不可为空", "NOTIFICATION_TEMPLATE_CULTURE_INVALID");
        }

        var trimmed = culture.Trim();
        if (trimmed.Length > MaxCultureLength)
        {
            throw new NotificationDomainException(
                $"通知模板文化不可超过 {MaxCultureLength} 字", "NOTIFICATION_TEMPLATE_CULTURE_INVALID");
        }

        try
        {
            // 校验为合法 BCP 47 文化标识，拒绝 "xx-99" 等非法值。
            _ = CultureInfo.GetCultureInfo(trimmed);
        }
        catch (CultureNotFoundException)
        {
            throw new NotificationDomainException(
                $"通知模板文化无效：{culture}", "NOTIFICATION_TEMPLATE_CULTURE_INVALID");
        }

        return new NotificationTemplateCulture(trimmed);
    }

    /// <summary>
    /// 尝试创建文化值对象，失败返回 <c>null</c>（用于 EF Core 值转换回退）。
    /// </summary>
    public static NotificationTemplateCulture? TryCreate(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        try
        {
            return Create(culture);
        }
        catch (NotificationDomainException)
        {
            return null;
        }
    }

    /// <summary>
    /// 判断当前文化是否为默认文化（zh-CN）。
    /// </summary>
    public bool IsDefault => string.Equals(Culture, Default.Culture, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as NotificationTemplateCulture);

    public bool Equals(NotificationTemplateCulture? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Culture, other.Culture, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Culture);

    public override string ToString() => Culture;

    public static bool operator ==(NotificationTemplateCulture? left, NotificationTemplateCulture? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(NotificationTemplateCulture? left, NotificationTemplateCulture? right)
        => !(left == right);
}
