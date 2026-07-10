namespace Leno.UserAuth.Application;

/// <summary>用户联系方式（未脱敏），仅供内部服务间调用。</summary>
public sealed class UserContactsDto
{
    public Guid UserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
