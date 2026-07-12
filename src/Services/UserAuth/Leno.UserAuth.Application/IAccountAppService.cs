using Leno.UserAuth.Application.DTOs;

namespace Leno.UserAuth.Application;

/// <summary>
/// 账户管理应用服务接口，提供外部登录绑定/解绑等账户操作。
/// </summary>
public interface IAccountAppService
{
    /// <summary>绑定外部登录（OAuth2 授权码交换后绑定）。</summary>
    Task BindExternalLoginAsync(Guid userId, BindExternalLoginDto dto, CancellationToken ct = default);

    /// <summary>解绑指定提供方的外部登录。</summary>
    Task UnbindExternalLoginAsync(Guid userId, string provider, CancellationToken ct = default);
}