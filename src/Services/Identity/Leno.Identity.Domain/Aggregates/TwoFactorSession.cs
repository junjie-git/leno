using Leno.SharedKernel.Abstractions;
using Leno.Identity.Domain.Exceptions;

namespace Leno.Identity.Domain.Aggregates;

/// <summary>
/// 双因子认证会话聚合根，承载 2FA 待验证状态的临时会话。
/// 从 UserAuth BC 的 ITwoFactorTempTokenStore 抽象演化而来（3.6 AuthN/AuthZ 拆分）：
/// 原实现基于 Redis 临时令牌（5 分钟 TTL），
/// 新聚合根支持会话生命周期管理与失败次数追踪，防止 2FA 暴力破解。
/// 生命周期：Pending → Verified / Expired / MaxAttemptsExceeded。
/// 单次登录触发 2FA 创建一个会话，验证成功或超时后不可复用。
/// </summary>
public sealed class TwoFactorSession : AggregateRoot
{
    /// <summary>双因子待验证临时令牌（不透明，Base64URL 编码 32 字节随机数）。</summary>
    public string TempToken { get; private set; } = string.Empty;

    /// <summary>关联用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>会话状态：Pending / Verified / Expired / MaxAttemptsExceeded。</summary>
    public TwoFactorSessionStatus Status { get; private set; }

    /// <summary>会话创建时间（UTC）。</summary>
    public new DateTime CreatedAt { get; private set; }

    /// <summary>会话过期时间（UTC），默认 5 分钟。</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>验证时间（UTC），未验证为 null。</summary>
    public DateTime? VerifiedAt { get; private set; }

    /// <summary>当前尝试次数。</summary>
    public int AttemptCount { get; private set; }

    /// <summary>最大尝试次数。</summary>
    public const int MaxAttempts = 5;

    /// <summary>EF Core 无参构造。</summary>
    private TwoFactorSession() { }

    private TwoFactorSession(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建新的 2FA 待验证会话。
    /// </summary>
    /// <param name="id">会话标识。</param>
    /// <param name="tempToken">不透明临时令牌。</param>
    /// <param name="userId">关联用户标识。</param>
    /// <param name="ttl">会话有效期，默认 5 分钟。</param>
    public static TwoFactorSession Create(
        Guid id,
        string tempToken,
        Guid userId,
        TimeSpan? ttl = null)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("双因子会话标识不可为空", "TWO_FACTOR_SESSION_ID_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(tempToken))
        {
            throw new IdentityDomainException("临时令牌不可为空", "TWO_FACTOR_TEMP_TOKEN_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("用户标识不可为空", "TWO_FACTOR_SESSION_USER_EMPTY");
        }

        var effectiveTtl = ttl ?? TimeSpan.FromMinutes(5);
        if (effectiveTtl <= TimeSpan.Zero)
        {
            throw new IdentityDomainException("会话有效期必须大于零", "TWO_FACTOR_SESSION_TTL_INVALID");
        }

        var now = DateTime.UtcNow;
        return new TwoFactorSession(id)
        {
            TempToken = tempToken,
            UserId = userId,
            Status = TwoFactorSessionStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.Add(effectiveTtl),
            AttemptCount = 0
        };
    }

    /// <summary>
    /// 记录一次验证尝试。若已达最大次数则置 MaxAttemptsExceeded。
    /// </summary>
    /// <returns>返回是否仍可继续尝试。</returns>
    public bool RecordAttempt()
    {
        if (Status != TwoFactorSessionStatus.Pending)
        {
            throw new IdentityDomainException("会话已结束，不可继续尝试", "TWO_FACTOR_SESSION_CLOSED");
        }

        if (IsExpired)
        {
            Status = TwoFactorSessionStatus.Expired;
            return false;
        }

        AttemptCount++;
        if (AttemptCount >= MaxAttempts)
        {
            Status = TwoFactorSessionStatus.MaxAttemptsExceeded;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 标记会话为已验证。
    /// </summary>
    public void MarkVerified()
    {
        if (Status != TwoFactorSessionStatus.Pending)
        {
            throw new IdentityDomainException("仅 Pending 状态的会话可标记为已验证", "TWO_FACTOR_SESSION_NOT_PENDING");
        }

        if (IsExpired)
        {
            Status = TwoFactorSessionStatus.Expired;
            throw new IdentityDomainException("会话已过期", "TWO_FACTOR_SESSION_EXPIRED");
        }

        Status = TwoFactorSessionStatus.Verified;
        VerifiedAt = DateTime.UtcNow;
    }

    /// <summary>会话是否已过期。</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>会话是否仍可验证（Pending 且未过期且未达最大尝试次数）。</summary>
    public bool CanVerify => Status == TwoFactorSessionStatus.Pending && !IsExpired && AttemptCount < MaxAttempts;
}

/// <summary>
/// 双因子会话状态枚举。
/// </summary>
public enum TwoFactorSessionStatus
{
    /// <summary>待验证。</summary>
    Pending = 1,

    /// <summary>已验证。</summary>
    Verified = 2,

    /// <summary>已过期。</summary>
    Expired = 3,

    /// <summary>尝试次数耗尽。</summary>
    MaxAttemptsExceeded = 4
}
