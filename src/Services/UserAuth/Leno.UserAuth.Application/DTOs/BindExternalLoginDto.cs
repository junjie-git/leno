namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 绑定外部登录请求 DTO。
/// </summary>
public sealed class BindExternalLoginDto
{
    /// <summary>OAuth2 提供方标识：google / wechat / alipay。</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>OAuth2 授权码。</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>OAuth2 回调地址。</summary>
    public string RedirectUri { get; init; } = string.Empty;
}