using Leno.UserAuth.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// 基于 Redis 的 OAuth2 state 存储实现。
/// Key 格式：<c>oauth:state:{state}</c>，Value 为 JSON 序列化的 <see cref="OAuthStatePayload"/>（provider|redirectUri）。
/// 使用 GETDEL 原子消费，避免并发重放。
/// </summary>
public sealed class RedisOAuthStateStore : IOAuthStateStore
{
    /// <summary>所有 OAuth state key 的统一前缀。</summary>
    public const string KeyPrefix = "oauth:state:";

    /// <summary>GETDEL 原子消费脚本：返回当前值并立即删除，避免并发重放。</summary>
    private const string ConsumeScriptText =
        "local current = redis.call('GETDEL', KEYS[1])\n" +
        "if not current then return false end\n" +
        "return current";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisOAuthStateStore> _logger;

    public RedisOAuthStateStore(IConnectionMultiplexer redis, ILogger<RedisOAuthStateStore> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StoreAsync(string state, string provider, string redirectUri, TimeSpan ttl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("state 不可为空", nameof(state));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("provider 不可为空", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new ArgumentException("redirectUri 不可为空", nameof(redirectUri));
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "state 有效期必须大于零");
        }

        var key = BuildKey(state);
        var payload = new OAuthStatePayload(provider, redirectUri);
        // 与历史格式兼容：仍使用 "provider|redirectUri" 字符串存储，避免破坏现有 Redis 数据
        var value = $"{payload.Provider}|{payload.RedirectUri}";
        var db = _redis.GetDatabase();

        await db.StringSetAsync(
            key,
            value,
            ttl,
            When.Always,
            CommandFlags.None).WaitAsync(ct);
    }

    /// <inheritdoc />
    public async Task<OAuthStateData?> ConsumeAsync(string state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var key = BuildKey(state);
        var db = _redis.GetDatabase();

        // 原子 GETDEL，避免并发重放
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
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var parts = raw.Split('|');
        if (parts.Length != 2)
        {
            _logger.LogWarning("OAuth state 数据格式无效：{RawValue}", raw);
            return null;
        }

        return new OAuthStateData(parts[0], parts[1]);
    }

    private static string BuildKey(string state) => $"{KeyPrefix}{state}";

    /// <summary>内部序列化载体（仅用于文档化字段顺序，实际仍以分隔符字符串持久化以兼容历史数据）。</summary>
    private sealed record OAuthStatePayload(string Provider, string RedirectUri);
}
