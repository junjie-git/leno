namespace Leno.UserCenter.Application;

/// <summary>
/// 用户联系方式（未脱敏），仅供具备 <c>internal-pii-read</c> 权限的内部服务调用。
/// 默认内部查询应返回 <see cref="UserContactsMaskedDto"/>，仅在显式声明 PII 读取权限时返回本 DTO。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class UserContactsDto
{
    public Guid UserId { get; set; }

    /// <summary>手机号（E.164 原始值），OAuth 注册用户可能为 null。</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>邮箱原始值，OAuth 注册用户（微信/支付宝）可能为 null。</summary>
    public string? Email { get; set; }
}
