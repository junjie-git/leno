namespace Leno.Notification.Domain.Services;

/// <summary>
/// 用户联系方式服务接口，定义在领域层，由基础设施层实现。
/// </summary>
public interface IUserContactService
{
    /// <summary>
    /// 查询用户联系方式。
    /// </summary>
    Task<UserContactInfo?> GetContactsAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// 用户联系方式信息。
/// </summary>
public sealed class UserContactInfo
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; set; }

    /// <summary>邮箱地址。</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>手机号。</summary>
    public string PhoneNumber { get; set; } = string.Empty;
}