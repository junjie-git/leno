using Leno.SharedKernel.Abstractions;
using Leno.Identity.Domain.Aggregates;

namespace Leno.Identity.Domain.Repositories;

/// <summary>
/// 刷新令牌仓储接口。
/// 从 UserAuth BC 的 IRefreshTokenStore 抽象演化而来（3.6 AuthN/AuthZ 拆分），
/// 现承载完整聚合根持久化与审计查询能力，而非仅 Redis 字符串映射。
/// </summary>
public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>按令牌字符串查询未撤销未过期的刷新令牌。</summary>
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>查询指定用户所有活跃刷新令牌。</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>撤销指定用户所有刷新令牌（账户禁用 / 登出 / 密码变更时调用）。</summary>
    Task RevokeAllByUserAsync(Guid userId, string reason, CancellationToken ct = default);
}
