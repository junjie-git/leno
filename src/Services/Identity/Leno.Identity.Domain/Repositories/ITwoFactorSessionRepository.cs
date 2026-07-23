using Leno.SharedKernel.Abstractions;
using Leno.Identity.Domain.Aggregates;

namespace Leno.Identity.Domain.Repositories;

/// <summary>
/// 双因子认证会话仓储接口。
/// 从 UserAuth BC 的 ITwoFactorTempTokenStore 抽象演化而来（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface ITwoFactorSessionRepository : IRepository<TwoFactorSession>
{
    /// <summary>按临时令牌查询活跃会话。</summary>
    Task<TwoFactorSession?> GetByTempTokenAsync(string tempToken, CancellationToken ct = default);

    /// <summary>清理指定用户所有过期或已结束的会话。</summary>
    Task CleanupExpiredByUserAsync(Guid userId, CancellationToken ct = default);
}
