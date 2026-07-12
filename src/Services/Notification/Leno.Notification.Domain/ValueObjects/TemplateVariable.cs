using Leno.Notification.Domain.Exceptions;

namespace Leno.Notification.Domain.ValueObjects;

/// <summary>
/// 模板变量值对象，描述模板中可替换的占位符变量。
/// </summary>
public sealed class TemplateVariable : IEquatable<TemplateVariable>
{
    /// <summary>变量名（不含花括号）。</summary>
    public string Name { get; private set; }

    /// <summary>是否必填。</summary>
    public bool Required { get; private set; }

    /// <summary>变量描述。</summary>
    public string Description { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private TemplateVariable()
    {
        Name = null!;
        Description = null!;
    }

    private TemplateVariable(string name, bool required, string description)
    {
        Name = name;
        Required = required;
        Description = description;
    }

    /// <summary>
    /// 工厂方法，创建模板变量。
    /// </summary>
    public static TemplateVariable Create(string name, bool required = false, string description = "")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new NotificationDomainException("变量名不可为空", "NOTIFICATION_VARIABLE_NAME_EMPTY");
        }

        if (name.Length > 64)
        {
            throw new NotificationDomainException("变量名不可超过 64 字", "NOTIFICATION_VARIABLE_NAME_TOO_LONG");
        }

        if (description.Length > 256)
        {
            throw new NotificationDomainException("变量描述不可超过 256 字", "NOTIFICATION_VARIABLE_DESC_TOO_LONG");
        }

        return new TemplateVariable(name.Trim(), required, description?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// 从纯变量名创建（兼容旧 List&lt;string&gt; 迁移）。
    /// </summary>
    public static TemplateVariable FromName(string name)
    {
        return Create(name, required: false, description: string.Empty);
    }

    public override bool Equals(object? obj) => Equals(obj as TemplateVariable);

    public bool Equals(TemplateVariable? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode() => Name.ToUpperInvariant().GetHashCode();

    public override string ToString() => $"{{{{{Name}}}}}{(Required ? " *" : "")}";
}