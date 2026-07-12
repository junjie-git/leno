using Leno.UserAuth.Application.DTOs;

namespace Leno.UserAuth.Application;

/// <summary>
/// 用户认证与个人资料应用服务，编排注册、登录、Token 刷新与资料维护用例。
/// </summary>
public interface IUserAppService
{
    /// <summary>注册账户并签发令牌。</summary>
    Task<TokenDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);

    /// <summary>账号密码登录并签发令牌。</summary>
    Task<TokenDto> LoginAsync(LoginDto dto, CancellationToken ct = default);

    /// <summary>使用刷新令牌换取新的访问与刷新令牌。</summary>
    Task<TokenDto> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>查询当前用户资料。</summary>
    Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct = default);

    /// <summary>修改当前用户资料。</summary>
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default);

    /// <summary>修改当前用户密码。</summary>
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken ct = default);

    /// <summary>获取 OAuth2 第三方授权登录 URL。</summary>
    Task<string> GetOAuthLoginUrlAsync(string provider, string redirectUri, CancellationToken ct = default);

    /// <summary>处理 OAuth2 回调，完成登录或注册并签发令牌。</summary>
    Task<TokenDto> HandleOAuthCallbackAsync(string provider, string code, string state, string redirectUri, CancellationToken ct = default);

    /// <summary>启用双因子认证，生成密钥与 QR 码 URI。</summary>
    Task<TwoFactorEnableResponseDto> EnableTwoFactorAsync(Guid userId, CancellationToken ct = default);

    /// <summary>确认双因子认证，验证 TOTP 码。</summary>
    Task ConfirmTwoFactorAsync(Guid userId, TwoFactorConfirmDto dto, CancellationToken ct = default);

    /// <summary>禁用双因子认证。</summary>
    Task DisableTwoFactorAsync(Guid userId, CancellationToken ct = default);

    /// <summary>双因子认证二次验证（登录流程），验证 TOTP 码并签发 JWT。</summary>
    Task<TokenDto> VerifyTwoFactorAsync(TwoFactorVerifyDto dto, CancellationToken ct = default);

    /// <summary>忘记密码：发送验证码/重置链接。</summary>
    Task ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default);

    /// <summary>重置密码。</summary>
    Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);
}
