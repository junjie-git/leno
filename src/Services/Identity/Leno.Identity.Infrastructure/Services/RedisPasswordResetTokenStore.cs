using System.Security.Cryptography;
using Leno.Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Identity.Infrastructure.Services;

/// <summary>
/// 基于 Redis 的密码重置令牌存储实现（Identity BC，Task A3 补齐）。
/// Key 格式：<c>reset:pwd:{token}</c>，Value 为 userId 字符串。
/// 使用 GETDEL 原子消费，避免并发重放。
/// <para>
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分），实现 <see cref="IPasswordResetTokenStore"/> 抽象。
/// </para>
/// </summary>
public sealed class RedisPasswordResetTokenStore : IPasswordResetTokenStore
{
    /// <summary>所有密码重置令牌 key 的统一前缀。</summary>
    public const string KeyPrefix = "reset:pwd:";

    /// <summary>GETDEL 原子消费脚本：返回当前值并立即删除，避免并发重放。</summary>
    private const string ConsumeScriptText =
        "local current = redis.call('GETDEL', KEYS[1])\n" +
        "if not current then return false end\n" +
        "return current";

    private const int TokenRandomBytes = 16; // 128 位熵

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisPasswordResetTokenStore> _logger;

    public RedisPasswordResetTokenStore(IConnectionMultiplexer redis, ILogger<RedisPasswordResetTokenStore> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> IssueAsync(Guid userId, TimeSpan ttl, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("用户标识不可为空", nameof(userId));
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "重置令牌有效期必须大于零");
        }

        // 使用密码学安全随机生成令牌，替代历史 Guid.NewGuid（P2-11 同源问题）
        var token = GenerateSecureToken();
        var key = BuildKey(token);
        var db = _redis.GetDatabase();

        await db.StringSetAsync(
            key,
            userId.ToString(),
            ttl,
            When.Always,
            CommandFlags.None).WaitAsync(ct);

        return token;
    }

    /// <inheritdoc />
    public async Task<Guid?> ValidateAndConsumeAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var key = BuildKey(token);
        var db = _redis.GetDatabase();

        var result = await db.ScriptEvaluateAsync(
            ConsumeScriptText,
            new RedisKey[] { key },
            Array.Empty<RedisValue>(),
            CommandFlags.None).WaitAsync(ct);

        if (result.IsNull)
        {
            return null;
        }

        var raw = (string?)result;
        if (!Guid.TryParse(raw, out var userId))
        {
            _logger.LogWarning("密码重置令牌对应的 UserId 解析失败：{RawValue}", raw);
            return null;
        }

        return userId;
    }

    private static string BuildKey(string token) => $"{KeyPrefix}{token}";

    private static string GenerateSecureToken()
    {
        Span<byte> buffer = stackalloc byte[TokenRandomBytes];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}
