using Leno.UserAuth.Domain.Exceptions;

namespace Leno.UserAuth.Domain.ValueObjects;

/// <summary>
/// 外部登录信息值对象，由 OAuth2 提供方回调后经 IExternalAuthService 填充。
/// 不可变记录，承载第三方身份提供方返回的用户信息。
/// 微信 / 支付宝不返回邮箱，<see cref="Email"/> 为 null；Google 通常返回邮箱。
/// </summary>
public sealed record ExternalLoginInfo
{
    /// <summary>OAuth2 提供方标识：google / wechat / alipay。</summary>
    public string Provider { get; init; }

    /// <summary>第三方平台用户唯一标识。</summary>
    public string ProviderUserId { get; init; }

    /// <summary>第三方平台返回的邮箱，微信 / 支付宝可能为 null。</summary>
    public string? Email { get; init; }

    /// <summary>第三方平台返回的昵称/姓名。</summary>
    public string Name { get; init; }

    /// <summary>第三方平台返回的头像 URL，可空。</summary>
    public string? AvatarUrl { get; init; }

    public ExternalLoginInfo(string provider, string providerUserId, string? email, string name, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            throw new UserAuthDomainException("第三方用户标识不可为空", "OAUTH_PROVIDER_USER_ID_EMPTY");
        }

        Provider = provider.Trim().ToLowerInvariant();
        ProviderUserId = providerUserId.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Name = name?.Trim() ?? string.Empty;
        AvatarUrl = avatarUrl?.Trim();
    }
}