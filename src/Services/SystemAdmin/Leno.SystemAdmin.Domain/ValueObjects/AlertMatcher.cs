using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>
/// 告警静默匹配器值对象，对应 Alertmanager silence matcher。
/// 描述一组 name/value（可选正则）的匹配条件，命中条件的告警在静默期内不再通知。
/// </summary>
public sealed class AlertMatcher : IEquatable<AlertMatcher>
{
    private const int MaxNameLength = 128;
    private const int MaxValueLength = 256;

    /// <summary>标签名，如 alertname、module、severity。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>标签值，如 HighErrorRate、Payment。</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>是否正则匹配；false 时为精确匹配。</summary>
    public bool IsRegex { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private AlertMatcher() { }

    public AlertMatcher(string name, string value, bool isRegex)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SystemAdminDomainException("匹配器名称不可为空", "ALERT_MATCHER_NAME_EMPTY");
        }
        if (name.Trim().Length > MaxNameLength)
        {
            throw new SystemAdminDomainException($"匹配器名称长度不可超过 {MaxNameLength} 字符", "ALERT_MATCHER_NAME_LENGTH");
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SystemAdminDomainException("匹配器值不可为空", "ALERT_MATCHER_VALUE_EMPTY");
        }
        if (value.Trim().Length > MaxValueLength)
        {
            throw new SystemAdminDomainException($"匹配器值长度不可超过 {MaxValueLength} 字符", "ALERT_MATCHER_VALUE_LENGTH");
        }

        Name = name.Trim();
        Value = value.Trim();
        IsRegex = isRegex;
    }

    /// <summary>
    /// 判断当前匹配器是否命中给定标签集合。
    /// 正则匹配在无效正则时回退为精确匹配，避免规则配置错误导致告警丢失。
    /// </summary>
    /// <param name="labels">告警标签集合。</param>
    /// <returns>命中返回 true；标签缺失或值不匹配返回 false。</returns>
    public bool Matches(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        if (!labels.TryGetValue(Name, out var labelValue))
        {
            return false;
        }

        if (IsRegex)
        {
            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(labelValue, Value);
            }
            catch (ArgumentException)
            {
                // 无效正则回退为精确匹配，避免规则配置错误导致告警丢失
                return string.Equals(labelValue, Value, StringComparison.Ordinal);
            }
        }

        return string.Equals(labelValue, Value, StringComparison.Ordinal);
    }

    public bool Equals(AlertMatcher? other)
    {
        if (other is null)
        {
            return false;
        }
        return string.Equals(Name, other.Name, StringComparison.Ordinal)
               && string.Equals(Value, other.Value, StringComparison.Ordinal)
               && IsRegex == other.IsRegex;
    }

    public override bool Equals(object? obj) => Equals(obj as AlertMatcher);

    public override int GetHashCode() => HashCode.Combine(Name, Value, IsRegex);
}
