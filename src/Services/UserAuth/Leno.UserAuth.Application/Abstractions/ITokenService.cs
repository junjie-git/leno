namespace Leno.UserAuth.Application.Abstractions;

/// <summary>
/// 访问令牌生成抽象，封装 JWT 签发细节。
/// 实现位于基础设施层（基于共享内核 JwtTokenGenerator），应用层不直接依赖 JWT 库。
/// </summary>
public interface ITokenService
{
    /// <summary>生成访问令牌。</summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="role">角色编码（多角色取最高权限角色）。</param>
    /// <param name="shopId">店铺标识，可空。</param>
    string GenerateAccessToken(Guid userId, string role, Guid? shopId = null);

    /// <summary>访问令牌有效期（秒）。</summary>
    int AccessTokenExpirySeconds { get; }
}
