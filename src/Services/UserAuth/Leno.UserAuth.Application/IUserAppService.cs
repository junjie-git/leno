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
}
