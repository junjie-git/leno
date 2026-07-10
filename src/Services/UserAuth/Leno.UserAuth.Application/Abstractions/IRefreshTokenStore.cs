namespace Leno.UserAuth.Application.Abstractions;

/// <summary>
/// 刷新令牌存储抽象，管理刷新令牌的签发、校验与轮换撤销。
/// 生产实现基于 Redis/数据库，本域基础设施层提供默认实现。
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>为指定用户签发新的刷新令牌。</summary>
    /// <returns>不透明的刷新令牌字符串。</returns>
    Task<string> IssueAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 校验刷新令牌并轮换：有效则撤销旧令牌并返回用户标识，无效返回 null。
    /// </summary>
    Task<Guid?> ValidateAndRotateAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>撤销指定用户的所有刷新令牌（账户禁用/登出时调用）。</summary>
    Task RevokeAllAsync(Guid userId, CancellationToken ct = default);
}
