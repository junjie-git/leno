using System.Globalization;
using System.Text.Json;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// Redis 缓存监控实现：INFO/Keyspace/SCAN/KeyDetail/Delete。
/// IServer.Keys 内部使用 SCAN，不阻塞；value 序列化后超 1MB 截断标记 truncated=true。
/// </summary>
public sealed class RedisCacheMonitorService : IRedisCacheMonitor
{
    private const int MaxValueBytes = 1024 * 1024; // 1MB
    private const int ScanMultiplier = 5;
    private static readonly JsonSerializerOptions DetailJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _redis;

    public RedisCacheMonitorService(IConnectionMultiplexer redis)
    {
        ArgumentNullException.ThrowIfNull(redis);
        _redis = redis;
    }

    /// <inheritdoc />
    public async Task<RedisInfoDto> GetInfoAsync(CancellationToken ct = default)
    {
        var endpoint = _redis.GetEndPoints().First();
        var server = _redis.GetServer(endpoint);
        ct.ThrowIfCancellationRequested();
        var infoSections = await server.InfoAsync();

        var serverSection = infoSections.FirstOrDefault(s => string.Equals(s.Key, "Server", StringComparison.Ordinal));
        var memorySection = infoSections.FirstOrDefault(s => string.Equals(s.Key, "Memory", StringComparison.Ordinal));
        var clientsSection = infoSections.FirstOrDefault(s => string.Equals(s.Key, "Clients", StringComparison.Ordinal));
        var statsSection = infoSections.FirstOrDefault(s => string.Equals(s.Key, "Stats", StringComparison.Ordinal));

        return new RedisInfoDto
        {
            RedisVersion = GetInfoValue(serverSection, "redis_version"),
            RedisMode = GetInfoValue(serverSection, "redis_mode"),
            Os = GetInfoValue(serverSection, "os"),
            ArchBits = GetInfoValue(serverSection, "arch_bits"),
            TcpPort = ParseInt(GetInfoValue(serverSection, "tcp_port")),
            UptimeInDays = ParseInt(GetInfoValue(serverSection, "uptime_in_days")),
            ConnectedClients = ParseInt(GetInfoValue(clientsSection, "connected_clients")),
            UsedMemoryHuman = GetInfoValue(memorySection, "used_memory_human"),
            UsedMemoryPeakHuman = GetInfoValue(memorySection, "used_memory_peak_human"),
            MaxmemoryHuman = GetInfoValue(memorySection, "maxmemory_human"),
            MemFragmentationRatio = ParseDouble(GetInfoValue(memorySection, "mem_fragmentation_ratio")),
            TotalConnectionsReceived = ParseLong(GetInfoValue(statsSection, "total_connections_received")),
            TotalCommandsProcessed = ParseLong(GetInfoValue(statsSection, "total_commands_processed")),
            KeyspaceHits = ParseLong(GetInfoValue(statsSection, "keyspace_hits")),
            KeyspaceMisses = ParseLong(GetInfoValue(statsSection, "keyspace_misses")),
            EvictedKeys = ParseLong(GetInfoValue(statsSection, "evicted_keys"))
        };
    }

    /// <inheritdoc />
    public async Task<List<KeyspaceDto>> GetKeyspacesAsync(CancellationToken ct = default)
    {
        var endpoint = _redis.GetEndPoints().First();
        var server = _redis.GetServer(endpoint);
        ct.ThrowIfCancellationRequested();
        var keyspaceInfo = await server.InfoAsync("keyspace");
        var keyspaceSection = keyspaceInfo.FirstOrDefault();

        var result = new List<KeyspaceDto>();
        for (int db = 0; db <= 15; db++)
        {
            var line = GetInfoValue(keyspaceSection, $"db{db}");
            if (string.IsNullOrEmpty(line))
            {
                result.Add(new KeyspaceDto { Db = db, Keys = 0, Expires = 0, AvgTtl = 0 });
                continue;
            }
            var parts = line.Split(',');
            result.Add(new KeyspaceDto
            {
                Db = db,
                Keys = ParseInt(ExtractValue(parts, "keys")),
                Expires = ParseInt(ExtractValue(parts, "expires")),
                AvgTtl = ParseInt(ExtractValue(parts, "avg_ttl"))
            });
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<PagedResult<RedisKeyDto>> ScanKeysAsync(int db, string pattern, string? type, int page, int pageSize, CancellationToken ct = default)
    {
        if (db < 0 || db > 15)
        {
            throw new ArgumentException("db 必须在 0-15 范围", nameof(db));
        }
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var endpoint = _redis.GetEndPoints().First();
        var server = _redis.GetServer(endpoint);
        var redisDb = _redis.GetDatabase(db);
        var scanLimit = pageSize * ScanMultiplier;
        var keys = new List<RedisKey>();

        await foreach (var key in server.KeysAsync(database: db, pattern: pattern, pageSize: scanLimit).WithCancellation(ct))
        {
            if (keys.Count >= scanLimit) break;
            keys.Add(key);
        }

        var filtered = new List<RedisKeyDto>();
        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();
            var keyType = await redisDb.KeyTypeAsync(key);
            var typeStr = keyType.ToString().ToLowerInvariant();
            if (!string.IsNullOrEmpty(type) && !string.Equals(typeStr, type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var ttl = await redisDb.KeyTimeToLiveAsync(key);
            var size = await GetKeySizeAsync(redisDb, key, typeStr);
            filtered.Add(new RedisKeyDto
            {
                Key = key.ToString(),
                Type = typeStr,
                Size = size,
                Ttl = ttl.HasValue ? (int)ttl.Value.TotalSeconds : -1
            });
        }

        var total = filtered.Count;
        var paged = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<RedisKeyDto>
        {
            Items = paged,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<RedisKeyDetailDto?> GetKeyDetailAsync(string key, int db, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (db < 0 || db > 15)
        {
            throw new ArgumentException("db 必须在 0-15 范围", nameof(db));
        }

        var redisDb = _redis.GetDatabase(db);
        var exists = await redisDb.KeyExistsAsync(key);
        if (!exists) return null;

        var keyType = await redisDb.KeyTypeAsync(key);
        var typeStr = keyType.ToString().ToLowerInvariant();
        var ttl = await redisDb.KeyTimeToLiveAsync(key);
        var size = await GetKeySizeAsync(redisDb, key, typeStr);
        var (value, truncated) = await GetKeyValueAsync(redisDb, key, typeStr);

        return new RedisKeyDetailDto
        {
            Key = key,
            Type = typeStr,
            Size = size,
            Ttl = ttl.HasValue ? (int)ttl.Value.TotalSeconds : -1,
            Value = value,
            Truncated = truncated
        };
    }

    /// <inheritdoc />
    public async Task<bool> DeleteKeyAsync(string key, int db, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (db < 0 || db > 15)
        {
            throw new ArgumentException("db 必须在 0-15 范围", nameof(db));
        }
        var redisDb = _redis.GetDatabase(db);
        return await redisDb.KeyDeleteAsync(key);
    }

    private static string GetInfoValue(IGrouping<string, KeyValuePair<string, string>>? section, string key)
        => section?.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal)).Value ?? string.Empty;

    private static int ParseInt(string value)
        => int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static long ParseLong(string value)
        => long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0L;

    private static double ParseDouble(string value)
        => double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0d;

    private static string ExtractValue(string[] parts, string key)
    {
        foreach (var part in parts)
        {
            var kv = part.Split('=');
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.Ordinal))
            {
                return kv[1];
            }
        }
        return "0";
    }

    private static async Task<int> GetKeySizeAsync(IDatabase db, RedisKey key, string type)
    {
        return type switch
        {
            "string" => (int)await db.StringLengthAsync(key),
            "hash" => (int)await db.HashLengthAsync(key),
            "list" => (int)await db.ListLengthAsync(key),
            "set" => (int)await db.SetLengthAsync(key),
            "zset" => (int)await db.SortedSetLengthAsync(key),
            "stream" => (int)await db.StreamLengthAsync(key),
            _ => 0
        };
    }

    private static async Task<(string Value, bool Truncated)> GetKeyValueAsync(IDatabase db, RedisKey key, string type)
    {
        string raw;
        switch (type)
        {
            case "string":
                raw = (string?)await db.StringGetAsync(key) ?? string.Empty;
                break;
            case "hash":
                var hashEntries = await db.HashGetAllAsync(key);
                var hashDict = hashEntries.ToDictionary(e => e.Name.ToString(), e => (string?)e.Value.ToString());
                raw = JsonSerializer.Serialize(hashDict, DetailJsonOptions);
                break;
            case "list":
                var listValues = await db.ListRangeAsync(key);
                raw = JsonSerializer.Serialize(listValues.Select(v => (string?)v.ToString()).ToArray(), DetailJsonOptions);
                break;
            case "set":
                var setMembers = await db.SetMembersAsync(key);
                raw = JsonSerializer.Serialize(setMembers.Select(v => (string?)v.ToString()).ToArray(), DetailJsonOptions);
                break;
            case "zset":
                var zsetMembers = await db.SortedSetRangeByRankWithScoresAsync(key);
                var zsetDict = zsetMembers.Select(m => new { key = (string?)m.Element.ToString(), score = m.Score }).ToArray();
                raw = JsonSerializer.Serialize(zsetDict, DetailJsonOptions);
                break;
            default:
                raw = $"{{\"type\":\"{type}\",\"message\":\"unsupported type\"}}";
                break;
        }

        var bytes = System.Text.Encoding.UTF8.GetByteCount(raw);
        if (bytes > MaxValueBytes)
        {
            var truncated = raw.Substring(0, MaxValueBytes);
            return (truncated, true);
        }
        return (raw, false);
    }
}
