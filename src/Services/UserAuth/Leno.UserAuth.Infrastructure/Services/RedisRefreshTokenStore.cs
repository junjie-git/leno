using System.Security.Cryptography;
using System.Text;
using Leno.Infrastructure.Auth;
using Leno.UserAuth.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// 基于 Redis 的刷新令牌存储，支持多实例共享与原子轮换。
/// Key 格式：<c>leno:userauth:refresh:{userId}:date:{token}</c>，Value 为 userId 字符串。
/// 使用 Lua 脚本原子 GETDEL 完成轮换，避免竞态重用。
/// 撤销某用户全部令牌时使用 SCAN 游标匹配 <c>leno:userauth:refresh:{userId}:date:*</c>。
/// </summary>
public sealed class RedisRefreshTokenStore : IRefreshTokenStore
{
    /// <summary>所有刷新令牌 key 的统一前缀。</summary>
    public const string KeyPrefix = "leno:userauth:refresh:";

    /// <summary>GETDEL 原子轮换脚本：返回当前值并立即删除，避免并发重用。</summary>
    /// <remarks>
    /// Redis 6.2+ 提供 GETDEL 原子命令；通过 EVAL 执行保证事务语义。
    /// 失败（key 不存在）返回 nil。
    /// </remarks>
    private const string RotateScriptText =
        "local current = redis.call('GETDEL', KEYS[1])\n" +
        "if not current then return false end\n" +
        "return current";

    private const int TokenPayloadBytes = 16 /* Guid */ + 32 /* random */;

    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _refreshTokenExpiry;
    private readonly ILogger<RedisRefreshTokenStore> _logger;

    public RedisRefreshTokenStore(
        IConnectionMultiplexer redis,
        TimeSpan refreshTokenExpiry,
        ILogger<RedisRefreshTokenStore> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);

        if (refreshTokenExpiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshTokenExpiry), "刷新令牌有效期必须大于零");
        }

        _redis = redis;
        _refreshTokenExpiry = refreshTokenExpiry;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("用户标识不可为空", nameof(userId));
        }

        var token = GenerateOpaqueToken(userId);
        var key = BuildKey(userId, token);
        var db = _redis.GetDatabase();

        await db.StringSetAsync(
            key,
            userId.ToString(),
            _refreshTokenExpiry,
            When.Always,
            CommandFlags.None).WaitAsync(ct);

        return token;
    }

    /// <inheritdoc />
    public async Task<Guid?> ValidateAndRotateAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        if (!TryDecodeToken(refreshToken, out var userId))
        {
            _logger.LogWarning("拒绝格式非法的刷新令牌");
            return null;
        }

        var key = BuildKey(userId, refreshToken);
        var db = _redis.GetDatabase();

        // 原子 GETDEL，避免并发重用同一令牌
        var result = await db.ScriptEvaluateAsync(
            RotateScriptText,
            new RedisKey[] { key },
            Array.Empty<RedisValue>(),
            CommandFlags.None).WaitAsync(ct);

        if (result.IsNull)
        {
            return null;
        }

        var raw = (string?)result;
        if (Guid.TryParse(raw, out var parsedUserId))
        {
            return parsedUserId;
        }

        _logger.LogWarning("刷新令牌对应的 UserId 解析失败：{RawValue}", raw);
        return null;
    }

    /// <inheritdoc />
    public async Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var db = _redis.GetDatabase();
        var pattern = $"{KeyPrefix}{userId}:date:*";
        var keys = new List<RedisKey>();

        // 仅在主节点上 SCAN，避免副本扫描结果与主节点不一致
        foreach (var endpoint in _redis.GetEndPoints())
        {
            IServer server;
            try
            {
                server = _redis.GetServer(endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法访问 Redis 端点 {Endpoint}，跳过", endpoint);
                continue;
            }

            if (server.IsReplica)
            {
                continue;
            }

            await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: 100).WithCancellation(ct))
            {
                keys.Add(key);
            }
        }

        if (keys.Count == 0)
        {
            return;
        }

        await db.KeyDeleteAsync(keys.ToArray(), CommandFlags.None).WaitAsync(ct);
    }

    private static string BuildKey(Guid userId, string token)
    {
        return $"{KeyPrefix}{userId}:date:{token}";
    }

    /// <summary>
    /// 生成不透明刷新令牌：Base64Url(userIdBytes|randomBytes)。
    /// 16 字节 UserId 用于在 Validate 时直接重建 key 而无需 SCAN。
    /// 32 字节随机数提供 256 位熵，防止碰撞。
    /// </summary>
    private static string GenerateOpaqueToken(Guid userId)
    {
        Span<byte> buffer = stackalloc byte[TokenPayloadBytes];
        if (!userId.TryWriteBytes(buffer))
        {
            throw new InvalidOperationException("无法序列化 UserId");
        }

        RandomNumberGenerator.Fill(buffer.Slice(16));
        return Base64UrlEncode(buffer);
    }

    private static bool TryDecodeToken(string token, out Guid userId)
    {
        try
        {
            var bytes = Base64UrlDecode(token);
            if (bytes.Length != TokenPayloadBytes)
            {
                userId = Guid.Empty;
                return false;
            }

            userId = new Guid(bytes.AsSpan(0, 16));
            return userId != Guid.Empty;
        }
        catch
        {
            userId = Guid.Empty;
            return false;
        }
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
