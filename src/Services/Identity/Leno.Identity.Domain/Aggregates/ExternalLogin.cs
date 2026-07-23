namespace Leno.Identity.Domain.Aggregates;

/// <summary>
/// 外部登录绑定实体，作为 User 聚合根的 owned collection。
/// 记录用户与第三方 OAuth2 身份提供方的绑定关系。
/// 唯一约束：同 Provider + ProviderUserId 组合全局唯一。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class ExternalLogin
{
    /// <summary>OAuth2 提供方标识：google / wechat / alipay。</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>第三方平台用户唯一标识。</summary>
    public string ProviderUserId { get; private set; } = string.Empty;

    /// <summary>绑定时的第三方邮箱快照。</summary>
    public string? Email { get; private set; }

    /// <summary>绑定时的第三方昵称快照。</summary>
    public string? Name { get; private set; }

    /// <summary>绑定时的第三方头像快照。</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>绑定时间（UTC）。</summary>
    public DateTime LinkedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private ExternalLogin() { }

    internal ExternalLogin(string provider, string providerUserId, string? email, string? name, string? avatarUrl)
    {
        Provider = provider;
        ProviderUserId = providerUserId;
        Email = email;
        Name = name;
        AvatarUrl = avatarUrl;
        LinkedAt = DateTime.UtcNow;
    }
}
