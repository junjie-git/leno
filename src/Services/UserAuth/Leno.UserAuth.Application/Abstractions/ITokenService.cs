namespace Leno.UserAuth.Application.Abstractions;

/// <summary>
/// 访问令牌生成抽象，封装 JWT 签发细节。
/// 实现位于基础设施层（基于共享内核 JwtTokenGenerator），应用层不直接依赖 JWT 库。
/// </summary>
public interface ITokenService
{
    /// <summary>生成访问令牌（单角色，向后兼容）。</summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="role">角色编码。</param>
    /// <param name="shopId">店铺标识，可空。</param>
    string GenerateAccessToken(Guid userId, string role, Guid? shopId = null);

    /// <summary>
    /// 生成访问令牌（多角色）。每个角色在 JWT 中添加独立的 role claim，
    /// 支持 <c>User.IsInRole</c> 多角色校验（P2-6）。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="roles">角色编码集合，至少一个。</param>
    /// <param name="shopId">店铺标识，可空。</param>
    string GenerateAccessToken(Guid userId, IReadOnlyCollection<string> roles, Guid? shopId = null);

    /// <summary>访问令牌有效期（秒）。</summary>
    int AccessTokenExpirySeconds { get; }
}
