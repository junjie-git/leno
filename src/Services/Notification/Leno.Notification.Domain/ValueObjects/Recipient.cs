using Leno.Notification.Domain.Exceptions;

namespace Leno.Notification.Domain.ValueObjects;

/// <summary>
/// 接收人值对象，封装用户标识与各渠道联系方式。
/// </summary>
public sealed class Recipient : IEquatable<Recipient>
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>邮箱地址。</summary>
    public string Email { get; private set; }

    /// <summary>手机号。</summary>
    public string PhoneNumber { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Recipient()
    {
        Email = null!;
        PhoneNumber = null!;
    }

    private Recipient(Guid userId, string email, string phoneNumber)
    {
        UserId = userId;
        Email = email;
        PhoneNumber = phoneNumber;
    }

    /// <summary>
    /// 工厂方法，创建接收人。
    /// </summary>
    public static Recipient Create(Guid userId, string? email = null, string? phoneNumber = null)
    {
        if (userId == Guid.Empty)
        {
            throw new NotificationDomainException("UserId 不可为空", "NOTIFICATION_RECIPIENT_USER_EMPTY");
        }

        return new Recipient(userId, email?.Trim() ?? string.Empty, phoneNumber?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// 验证指定渠道是否具备联系方式。
    /// </summary>
    public bool HasContactFor(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.Email => !string.IsNullOrWhiteSpace(Email),
            NotificationChannel.Sms => !string.IsNullOrWhiteSpace(PhoneNumber),
            _ => true // InApp 不需要单独联系方式
        };
    }

    /// <summary>
    /// 获取指定渠道的联系方式字符串。
    /// </summary>
    public string? GetContactFor(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.Email => string.IsNullOrWhiteSpace(Email) ? null : Email,
            NotificationChannel.Sms => string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber,
            _ => null
        };
    }

    public override bool Equals(object? obj) => Equals(obj as Recipient);

    public bool Equals(Recipient? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return UserId == other.UserId
               && string.Equals(Email, other.Email, StringComparison.OrdinalIgnoreCase)
               && string.Equals(PhoneNumber, other.PhoneNumber, StringComparison.Ordinal);
    }

    // P2-39：GetHashCode 必须与 Equals 使用相同的比较算法，保证两个 Equals 相等的 Recipient 哈希值相等。
    // Equals 中 Email 用 OrdinalIgnoreCase、PhoneNumber 用 Ordinal 比较，故哈希也用对应 StringComparer。
    public override int GetHashCode()
        => HashCode.Combine(
            UserId,
            StringComparer.OrdinalIgnoreCase.GetHashCode(Email),
            StringComparer.Ordinal.GetHashCode(PhoneNumber));

    public override string ToString() => $"Recipient({UserId})";
}