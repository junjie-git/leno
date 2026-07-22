namespace Leno.SharedContracts.Integration.Dto;

/// <summary>
/// 用户联系方式共享 DTO（D2.4 ACL 模式去重）。
/// 各 BC 的 UserContact ACL 防腐层统一返回此类型，消除 Notification / Order / ReviewAfterSales / Promotion 4 BC 重复定义。
/// 字段为各 BC 需求的超集：手机号与邮箱均可为空（OAuth 注册用户可能仅有 OpenId），昵称为可选展示字段。
/// </summary>
public sealed class UserContactDto
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>邮箱地址，OAuth 注册用户（微信/支付宝）可能为空。</summary>
    public string? Email { get; init; }

    /// <summary>手机号（E.164 原始值），OAuth 注册用户可能为空。</summary>
    public string? Phone { get; init; }

    /// <summary>用户昵称（用于通知文案展示，可选）。</summary>
    public string? Nickname { get; init; }

    /// <summary>
    /// 创建与已有 <c>UserContactInfo</c> 兼容的实例（仅 UserId/Email/Phone，无 Nickname）。
    /// 各 BC 迁移期可使用此工厂方法，避免一次性破坏现有调用点。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="email">邮箱地址，可为空。</param>
    /// <param name="phone">手机号，可为空。</param>
    public static UserContactDto Create(Guid userId, string? email, string? phone)
        => new() { UserId = userId, Email = email, Phone = phone };
}
