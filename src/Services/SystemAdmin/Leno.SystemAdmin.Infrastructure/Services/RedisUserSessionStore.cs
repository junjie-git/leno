using System.Text.Json;
using Leno.Infrastructure.Abstractions.Sessions;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// Redis 用户会话存储实现：Hash + Set + ZSet 三层结构。
/// session:{sessionId} → Hash 单会话详情 TTL 24h
/// session:user:{userId} → Set 用户所有 sessionId TTL 24h
/// session:index → ZSet (score=loginAt unix) 全局会话时间索引
/// </summary>
public sealed class RedisUserSessionStore : IUserSessionStore
{
    private static readonly JsonSerializerOptions RolesJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _sessionTtl = TimeSpan.FromHours(24);

    public RedisUserSessionStore(IConnectionMultiplexer redis)
    {
        ArgumentNullException.ThrowIfNull(redis);
        _redis = redis;
    }

    /// <inheritdoc />
    public async Task RecordAsync(OnlineUserSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(session.SessionId))
        {
            throw new ArgumentException("SessionId 不可为空", nameof(session));
        }
        if (session.UserId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(session));
        }

        var db = _redis.GetDatabase();
        var sessionKey = $"session:{session.SessionId}";
        var userIndexKey = $"session:user:{session.UserId}";
        var globalIndexKey = "session:index";
        var loginAtTs = new DateTimeOffset(session.LoginAt).ToUnixTimeSeconds();

        var batch = db.CreateBatch();
        var entries = new HashEntry[]
        {
            new("userId", session.UserId.ToString()),
            new("username", session.Username),
            new("roles", JsonSerializer.Serialize(session.Roles, RolesJsonOptions)),
            new("ipAddress", session.IpAddress),
            new("geoLocation", session.GeoLocation ?? string.Empty),
            new("browser", session.Browser),
            new("os", session.Os),
            new("tokenPreview", session.TokenPreview),
            new("deviceFingerprint", session.DeviceFingerprint ?? string.Empty),
            new("requestCount", session.RequestCount.ToString()),
            new("loginAt", session.LoginAt.ToString("O")),
            new("lastActivityAt", session.LastActivityAt.ToString("O")),
            new("isAnomaly", session.IsAnomaly.ToString())
        };
        _ = batch.HashSetAsync(sessionKey, entries);
        _ = batch.KeyExpireAsync(sessionKey, _sessionTtl);
        _ = batch.SetAddAsync(userIndexKey, session.SessionId);
        _ = batch.KeyExpireAsync(userIndexKey, _sessionTtl);
        _ = batch.SortedSetAddAsync(globalIndexKey, session.SessionId, loginAtTs);
        batch.Execute();

        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<List<OnlineUserSession>> QueryAsync(OnlineUserQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var db = _redis.GetDatabase();
        var fromTs = query.LoginAtFrom.HasValue
            ? new DateTimeOffset(query.LoginAtFrom.Value).ToUnixTimeSeconds()
            : 0;
        var toTs = query.LoginAtTo.HasValue
            ? new DateTimeOffset(query.LoginAtTo.Value).ToUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var sessionIds = await db.SortedSetRangeByScoreAsync(
            "session:index",
            fromTs,
            toTs,
            order: Order.Descending,
            skip: (query.Page - 1) * query.PageSize,
            take: query.PageSize);

        var sessions = new List<OnlineUserSession>();
        foreach (var sid in sessionIds)
        {
            ct.ThrowIfCancellationRequested();
            var sidStr = sid.ToString();
            var hash = await db.HashGetAllAsync($"session:{sidStr}");
            if (hash.Length == 0) continue;
            sessions.Add(MapFromHash(sidStr, hash));
        }

        if (!string.IsNullOrEmpty(query.Username))
        {
            sessions = sessions.Where(s => s.Username.Contains(query.Username, StringComparison.Ordinal)).ToList();
        }
        if (!string.IsNullOrEmpty(query.IpAddress))
        {
            sessions = sessions.Where(s => s.IpAddress.Contains(query.IpAddress, StringComparison.Ordinal)).ToList();
        }

        return sessions;
    }

    /// <inheritdoc />
    public async Task<OnlineUserSession?> GetByIdAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var db = _redis.GetDatabase();
        var hash = await db.HashGetAllAsync($"session:{sessionId}");
        if (hash.Length == 0) return null;
        return MapFromHash(sessionId, hash);
    }

    /// <inheritdoc />
    public async Task<OnlineUserStats> GetStatsAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var total = await db.SortedSetLengthAsync("session:index");
        var since24h = DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeSeconds();
        var logins24h = await db.SortedSetLengthAsync("session:index", since24h);

        var sessionIds = await db.SortedSetRangeByScoreAsync("session:index");
        int anomalies = 0;
        foreach (var sid in sessionIds)
        {
            ct.ThrowIfCancellationRequested();
            var isAnomaly = (string?)await db.HashGetAsync($"session:{sid}", "isAnomaly");
            if (string.Equals(isAnomaly, bool.TrueString, StringComparison.Ordinal))
            {
                anomalies++;
            }
        }

        return new OnlineUserStats
        {
            Total = (int)total,
            Logins24h = (int)logins24h,
            Anomalies = anomalies
        };
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var db = _redis.GetDatabase();
        var userIdStr = (string?)await db.HashGetAsync($"session:{sessionId}", "userId");

        var batch = db.CreateBatch();
        _ = batch.KeyDeleteAsync($"session:{sessionId}");
        if (Guid.TryParse(userIdStr, out var userId) && userId != Guid.Empty)
        {
            _ = batch.SetRemoveAsync($"session:user:{userId}", sessionId);
        }
        _ = batch.SortedSetRemoveAsync("session:index", sessionId);
        batch.Execute();

        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync($"session:{sessionId}");
    }

    private static OnlineUserSession MapFromHash(string sessionId, HashEntry[] hash)
    {
        var map = hash.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
        return new OnlineUserSession
        {
            SessionId = sessionId,
            UserId = Guid.TryParse(GetValue(map, "userId"), out var uid) ? uid : Guid.Empty,
            Username = GetValue(map, "username"),
            Roles = DeserializeRoles(GetValue(map, "roles")),
            IpAddress = GetValue(map, "ipAddress"),
            GeoLocation = string.IsNullOrEmpty(GetValue(map, "geoLocation")) ? null : GetValue(map, "geoLocation"),
            Browser = GetValue(map, "browser"),
            Os = GetValue(map, "os"),
            TokenPreview = GetValue(map, "tokenPreview"),
            DeviceFingerprint = string.IsNullOrEmpty(GetValue(map, "deviceFingerprint")) ? null : GetValue(map, "deviceFingerprint"),
            RequestCount = int.TryParse(GetValue(map, "requestCount"), out var rc) ? rc : 0,
            LoginAt = DateTime.TryParse(GetValue(map, "loginAt"), out var la) ? la : DateTime.UtcNow,
            LastActivityAt = DateTime.TryParse(GetValue(map, "lastActivityAt"), out var laa) ? laa : DateTime.UtcNow,
            IsAnomaly = string.Equals(GetValue(map, "isAnomaly"), bool.TrueString, StringComparison.Ordinal)
        };
    }

    private static string GetValue(Dictionary<string, string> map, string key)
        => map.TryGetValue(key, out var v) ? v : string.Empty;

    private static List<string> DeserializeRoles(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, RolesJsonOptions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
