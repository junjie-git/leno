using System.Text.RegularExpressions;

namespace Leno.UserCenter.Domain.ValueObjects;

/// <summary>
/// 收货详细地址值对象，校验长度 5–200 字符，不可变。
/// 仅承载详细地址文本，省/市/区由 Address 聚合直接持有字符串。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed partial record AddressDetail
{
    private const int MinLength = 5;
    private const int MaxLength = 200;

    /// <summary>详细地址文本。</summary>
    public string Value { get; }

    private AddressDetail(string value)
    {
        Value = value;
    }

    public static AddressDetail Create(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            throw new ArgumentException("详细地址不可为空", nameof(detail));
        }

        var trimmed = detail.Trim();
        if (trimmed.Length is < MinLength or > MaxLength)
        {
            throw new ArgumentException($"详细地址长度须为 {MinLength}-{MaxLength} 字符", nameof(detail));
        }

        if (!ValidDetailPattern().IsMatch(trimmed))
        {
            throw new ArgumentException("详细地址包含非法字符", nameof(detail));
        }

        return new AddressDetail(trimmed);
    }

    [GeneratedRegex(@"^[\p{L}\p{N}\s\-\.,()（）#号巷弄楼室栋单元]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidDetailPattern();

    public override string ToString() => Value;
}
