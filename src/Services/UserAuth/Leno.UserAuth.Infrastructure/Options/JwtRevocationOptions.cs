namespace Leno.UserAuth.Infrastructure.Options;

/// <summary>
/// JWT 吊销服务配置项。
/// 缺省值与原 <c>JwtRevocationService.cs</c> 固定 <c>TimeSpan.FromHours(2)</c> 完全对齐（零行为变更门禁）。
/// </summary>
public sealed class JwtRevocationOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "UserAuth:JwtRevocation";

    /// <summary>
    /// 访问令牌有效期（分钟）。
    /// 用于动态计算用户级黑名单 TTL，应与 <c>JwtOptions.AccessTokenExpiryMinutes</c> 保持一致或略小。
    /// 缺省值 115：比 JWT 默认 120 分钟少 5 分钟，留出缓冲。
    /// </summary>
    public int AccessTokenTtlMinutes { get; set; } = 115;

    /// <summary>
    /// 刷新令牌有效期（分钟）。
    /// 缺省值 10080（7 天 × 24 小时 × 60 分钟），与 <c>JwtOptions.RefreshTokenExpiryDays</c> 默认 7 天对齐。
    /// </summary>
    public int RefreshTokenTtlMinutes { get; set; } = 10080;

    /// <summary>
    /// 黑名单缓冲时间（分钟）。
    /// 在访问令牌有效期基础上额外增加的缓冲，确保令牌过期前黑名单不会先过期。
    /// 缺省值 5：与 <see cref="AccessTokenTtlMinutes"/> 115 合计 120 分钟 = 2 小时，与原固定 TTL 完全对齐。
    /// </summary>
    public int BlacklistBufferMinutes { get; set; } = 5;
}
