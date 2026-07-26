using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// 认证应用服务接口（Identity BC，Task A2 补齐）。
/// 承载注册、登录、刷新令牌与登出用例，供 A3 AuthController 消费。
/// <para>
/// LoginAsync / RefreshTokenAsync / LogoutAsync 委托 <see cref="IAuthenticationAppService"/> 既有实现，
/// RegisterAsync 为本接口新增方法。
/// </para>
/// </summary>
public interface IAuthAppService
{
    /// <summary>注册账户并签发令牌。</summary>
    /// <param name="request">注册请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含访问与刷新令牌的响应。</returns>
    Task<TokenDto> RegisterAsync(RegisterDto request, CancellationToken ct = default);

    /// <summary>账号密码登录并签发令牌。</summary>
    /// <param name="request">登录请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含访问与刷新令牌的响应。</returns>
    Task<TokenDto> LoginAsync(LoginDto request, CancellationToken ct = default);

    /// <summary>使用刷新令牌换取新的访问与刷新令牌。</summary>
    /// <param name="request">刷新请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含新访问与新刷新令牌的响应。</returns>
    Task<TokenDto> RefreshTokenAsync(RefreshTokenDto request, CancellationToken ct = default);

    /// <summary>登出，吊销指定用户的所有活跃刷新令牌。</summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task LogoutAsync(Guid userId, CancellationToken ct = default);
}
