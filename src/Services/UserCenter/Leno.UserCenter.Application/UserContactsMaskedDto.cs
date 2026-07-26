namespace Leno.UserCenter.Application;

/// <summary>
/// 用户联系方式（已脱敏），作为内部查询的默认返回 DTO。
/// 手机号保留前 3 后 4 位（如 138****1234），邮箱保留首字符与域名（如 a***@example.com）。
/// 即使内部 API 中间件配置错误，默认响应也不泄露完整 PII。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class UserContactsMaskedDto
{
    public Guid UserId { get; set; }

    /// <summary>脱敏手机号（前 3 后 4），无手机号时为 null。</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>脱敏邮箱（首字符 + 域名），无邮箱时为 null。</summary>
    public string? Email { get; set; }

    /// <summary>
    /// 从原始联系方式构造脱敏 DTO。
    /// 手机号：长度 &gt; 7 时保留前 3 后 4，否则全掩码为 ****；空值保持 null。
    /// 邮箱：含 @ 时保留首字符与域名（a***@example.com），无 @ 或长度 ≤ 1 时掩码为 ****；空值保持 null。
    /// </summary>
    public static UserContactsMaskedDto FromContacts(Guid userId, string? phoneNumber, string? email)
    {
        return new UserContactsMaskedDto
        {
            UserId = userId,
            PhoneNumber = MaskPhone(phoneNumber),
            Email = MaskEmail(email)
        };
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone))
        {
            return null;
        }

        if (phone.Length <= 7)
        {
            return "****";
        }

        var head = phone[..3];
        var tail = phone[^4..];
        return $"{head}****{tail}";
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex >= email.Length - 1)
        {
            return "****";
        }

        var head = email[..1];
        var domain = email[atIndex..];
        return $"{head}***{domain}";
    }
}
