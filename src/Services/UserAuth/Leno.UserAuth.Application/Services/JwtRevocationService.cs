using Leno.UserAuth.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// UserAuth 域 JWT 吊销服务，通过 Redis 写入黑名单（与网关共用 Redis 实例）。
/// </summary>
public sealed class JwtRevocationService : IJwtRevocationService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<JwtRevocationService> _logger;

    public JwtRevocationService(IConnectionMultiplexer redis, ILogger<JwtRevocationService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"leno:jwt:blacklist:{jti}", "1", ttl);
        _logger.LogInformation("用户登出，JWT 已吊销 Jti={Jti}", jti);
    }
}
