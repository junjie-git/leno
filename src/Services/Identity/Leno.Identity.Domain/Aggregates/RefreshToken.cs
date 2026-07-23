using Leno.SharedKernel.Abstractions;
using Leno.Identity.Domain.Exceptions;

namespace Leno.Identity.Domain.Aggregates;

/// <summary>
/// 刷新令牌聚合根，承载刷新令牌轮换与撤销状态。
/// 从 UserAuth BC 的 IRefreshTokenStore 抽象演化而来（3.6 AuthN/AuthZ 拆分）：
/// 原实现基于 Redis 字符串键值对，仅承载 token → userId 映射；
/// 新聚合根支持完整生命周期追踪与撤销原因审计，便于安全合规审计与多设备管理。
/// 单次签发对应一条记录，旋转时旧令牌标记 Revoked 而非物理删除，保留审计轨迹。
/// </summary>
public sealed class RefreshToken : AggregateRoot
{
    /// <summary>令牌字符串（不透明，Base64URL 编码 32 字节随机数）。</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>签发时间（UTC）。</summary>
    public DateTime IssuedAt { get; private set; }

    /// <summary>过期时间（UTC）。</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>撤销时间（UTC），未撤销为 null。</summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>撤销原因：Rotated / Logout / PasswordChange / Disable / AdminRevoke。</summary>
    public string? RevokeReason { get; private set; }

    /// <summary>替换本令牌的新令牌标识（轮换场景），未轮换为 null。</summary>
    public Guid? ReplacedById { get; private set; }

    /// <summary>是否已撤销。</summary>
    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>是否已过期。</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>是否仍可用（未撤销且未过期）。</summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    /// <summary>EF Core 无参构造。</summary>
    private RefreshToken() { }

    private RefreshToken(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，签发新的刷新令牌。
    /// </summary>
    /// <param name="id">令牌聚合标识（与 Token 字符串独立）。</param>
    /// <param name="token">不透明令牌字符串。</param>
    /// <param name="userId">所属用户标识。</param>
    /// <param name="expiresAt">过期时间（UTC）。</param>
    public static RefreshToken Create(Guid id, string token, Guid userId, DateTime expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("刷新令牌标识不可为空", "REFRESH_TOKEN_ID_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new IdentityDomainException("刷新令牌字符串不可为空", "REFRESH_TOKEN_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("用户标识不可为空", "REFRESH_TOKEN_USER_EMPTY");
        }

        if (expiresAt <= DateTime.UtcNow)
        {
            throw new IdentityDomainException("刷新令牌过期时间必须晚于当前时间", "REFRESH_TOKEN_EXPIRY_INVALID");
        }

        return new RefreshToken(id)
        {
            Token = token,
            UserId = userId,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// 轮换令牌：标记当前令牌为已撤销，记录替换关系。
    /// </summary>
    /// <param name="replacedById">新令牌的聚合标识。</param>
    public void Rotate(Guid replacedById)
    {
        if (IsRevoked)
        {
            throw new IdentityDomainException("令牌已撤销，不可重复撤销", "REFRESH_TOKEN_ALREADY_REVOKED");
        }

        if (replacedById == Guid.Empty)
        {
            throw new IdentityDomainException("新令牌标识不可为空", "REFRESH_TOKEN_REPLACED_BY_EMPTY");
        }

        RevokedAt = DateTime.UtcNow;
        RevokeReason = "Rotated";
        ReplacedById = replacedById;
    }

    /// <summary>
    /// 撤销令牌（登出 / 密码变更 / 账户禁用 / 管理员撤销）。
    /// </summary>
    /// <param name="reason">撤销原因。</param>
    public void Revoke(string reason)
    {
        if (IsRevoked)
        {
            // 幂等：已撤销时直接返回，避免重复操作抛异常
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new IdentityDomainException("撤销原因不可为空", "REFRESH_TOKEN_REVOKE_REASON_EMPTY");
        }

        RevokedAt = DateTime.UtcNow;
        RevokeReason = reason.Trim();
    }
}
