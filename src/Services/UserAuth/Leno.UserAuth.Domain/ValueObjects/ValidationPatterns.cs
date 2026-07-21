using System.Text.RegularExpressions;

namespace Leno.UserAuth.Domain.ValueObjects;

/// <summary>
/// 用户名格式校验模式，供 User 聚合与 RegisterDtoValidator 共享，消除 DRY 违反（P2-7）。
/// 仅允许字母、数字与下划线，长度 3-32。
/// </summary>
public static partial class UsernamePattern
{
    /// <summary>正则字符串，供 FluentValidation Matches() 使用。</summary>
    public const string PatternStr = @"^[a-zA-Z0-9_]{3,32}$";

    /// <summary>校验失败时的错误消息。</summary>
    public const string ErrorMessage = "用户名仅允许字母、数字与下划线，长度 3-32";

    /// <summary>编译期生成的正则实例（CultureInvariant）。</summary>
    [GeneratedRegex(@"^[a-zA-Z0-9_]{3,32}$", RegexOptions.CultureInvariant)]
    public static partial Regex GetRegex();
}

/// <summary>
/// 邮箱格式校验模式，供 User 聚合与 RegisterDtoValidator 共享，消除 DRY 违反（P2-7）。
/// </summary>
public static partial class EmailPattern
{
    /// <summary>正则字符串。</summary>
    public const string PatternStr = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    /// <summary>校验失败时的错误消息。</summary>
    public const string ErrorMessage = "邮箱格式不正确";

    /// <summary>编译期生成的正则实例（CultureInvariant + IgnoreCase）。</summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    public static partial Regex GetRegex();
}

/// <summary>
/// 手机号格式校验模式（E.164），供 User 聚合与 RegisterDtoValidator 共享，消除 DRY 违反（P2-7）。
/// </summary>
public static partial class PhonePattern
{
    /// <summary>正则字符串。</summary>
    public const string PatternStr = @"^\+[1-9]\d{1,14}$";

    /// <summary>校验失败时的错误消息。</summary>
    public const string ErrorMessage = "手机号须为 E.164 格式（如 +8613800138000）";

    /// <summary>编译期生成的正则实例（CultureInvariant）。</summary>
    [GeneratedRegex(@"^\+[1-9]\d{1,14}$", RegexOptions.CultureInvariant)]
    public static partial Regex GetRegex();
}
