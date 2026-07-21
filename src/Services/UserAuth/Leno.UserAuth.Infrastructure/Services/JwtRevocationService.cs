using Leno.UserAuth.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// UserAuth 域 JWT 吊销服务，通过 Redis 写入黑名单（与网关共用 Redis 实例）。
/// 实现位于基础设施层，避免应用层直接依赖 StackExchange.Redis。
/// </summary>
public sealed class JwtRevocationService : IJwtRevocationService
{
    /// <summary>JWT 黑名单 key 前缀。</summary>
    public const string BlacklistKeyPrefix = "leno:jwt:blacklist:";

    /// <summary>用户级 JWT 黑名单 key 前缀，用于按 userId 批量撤销已签发的所有 JWT。</summary>
    public const string UserBlacklistKeyPrefix = "leno:jwt:user-blacklist:";

    /// <summary>用户级黑名单默认 TTL：与 JWT AccessToken 最大有效期对齐（默认 120 分钟）。</summary>
    private static readonly TimeSpan UserBlacklistTtl = TimeSpan.FromHours(2);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<JwtRevocationService> _logger;

    public JwtRevocationService(IConnectionMultiplexer redis, ILogger<JwtRevocationService> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            throw new ArgumentException("JWT 标识不可为空", nameof(jti));
        }

        if (ttl <= TimeSpan.Zero)
        {
            // 令牌已过期，无需写入黑名单
            return;
        }

        var db = _redis.GetDatabase();
        await db.StringSetAsync(
            $"{BlacklistKeyPrefix}{jti}",
            "1",
            ttl,
            When.Always,
            CommandFlags.None).WaitAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("用户登出，JWT 已吊销 Jti={Jti}", jti);
    }

    /// <inheritdoc />
    public async Task RevokeUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var db = _redis.GetDatabase();
        await db.StringSetAsync(
            $"{UserBlacklistKeyPrefix}{userId}",
            "1",
            UserBlacklistTtl,
            When.Always,
            CommandFlags.None).WaitAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("用户 {UserId} 所有 JWT 已加入黑名单，TTL={Ttl}", userId, UserBlacklistTtl);
    }
}
