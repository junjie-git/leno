using System.IO.Hashing;
using System.Text;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Infrastructure.Caching;

/// <summary>
/// 基于 Redis Bitmap 的布隆过滤器实现。
/// 使用多个哈希函数将 key 映射到位图的多个位置，实现 O(1) 的成员存在性检查。
/// 默认参数：预期元素数量 10,000,000，误判率 0.01（1%）。
/// 计算公式：m = -n * ln(p) / (ln(2)^2)，k = (m / n) * ln(2)
/// </summary>
public sealed class RedisBloomFilter : IBloomFilter
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisBloomFilter> _logger;
    private readonly string _redisKey;
    private readonly int _bitSize;
    private readonly int _hashCount;

    /// <summary>
    /// Lua 脚本：批量设置多个 bit，将多次 StringSetBitAsync 合并为单次网络往返。
    /// KEYS[1] = Redis bitmap key，ARGV[1..N] = bit 偏移量。
    /// 返回设置的 bit 数量。
    /// </summary>
    private static readonly string _batchSetBitsScript = @"
for i = 1, #ARGV do
    redis.call('SETBIT', KEYS[1], ARGV[i], 1)
end
return #ARGV
";

    /// <summary>
    /// Lua 脚本：批量检查多个 bit 是否全为 1，将多次 StringGetBitAsync 合并为单次网络往返。
    /// KEYS[1] = Redis bitmap key，ARGV[1..N] = bit 偏移量。
    /// 全部为 1 返回 1（可能存在），任一为 0 返回 0（一定不存在）。
    /// 任务 2.2.1：替代原 MightContainAsync 中 N 次串行 StringGetBitAsync 的 Task.WhenAll 实现，
    /// 将 N 次网络往返降为 1 次 EVAL。
    /// </summary>
    private static readonly string _batchGetBitsScript = @"
for i = 1, #ARGV do
    if redis.call('GETBIT', KEYS[1], ARGV[i]) == 0 then
        return 0
    end
end
return 1
";

    /// <summary>
    /// 默认参数：1000 万元素，1% 误判率
    /// m = -10000000 * ln(0.01) / (ln(2)^2) ≈ 95,850,583 bits ≈ 11.4 MB
    /// k = (95850583 / 10000000) * ln(2) ≈ 7
    /// </summary>
    private const int DefaultExpectedElements = 10_000_000;
    private const double DefaultFalsePositiveRate = 0.01;

    public RedisBloomFilter(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisBloomFilter> logger,
        string redisKey = "leno:bloom",
        int? expectedElements = null,
        double? falsePositiveRate = null)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        _database = connectionMultiplexer.GetDatabase();
        _logger = logger;
        _redisKey = redisKey;

        var n = expectedElements ?? DefaultExpectedElements;
        var p = falsePositiveRate ?? DefaultFalsePositiveRate;

        _bitSize = CalculateBitSize(n, p);
        _hashCount = CalculateHashCount(_bitSize, n);

        _logger.LogInformation(
            "RedisBloomFilter 初始化完成: BitSize={BitSize}, HashCount={HashCount}, RedisKey={RedisKey}",
            _bitSize, _hashCount, _redisKey);
    }

    public async Task AddAsync(string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var positions = GetHashPositions(key);

        // 修复 T38：使用 Lua 脚本批量设置 bit，将 N 次 StringSetBitAsync 合并为 1 次网络往返。
        // 原实现循环调用 StringSetBitAsync（默认 7 次），即使 Task.WhenAll 允许管道化，
        // 仍有多次网络往返开销；Lua 脚本保证单次往返且原子执行。
        var args = positions.Select(p => (RedisValue)p).ToArray();
        await _database.ScriptEvaluateAsync(
            _batchSetBitsScript,
            new RedisKey[] { _redisKey },
            args).ConfigureAwait(false);

        _logger.LogDebug("BloomFilter 添加 key: {Key}, Positions: {Positions}", key, string.Join(",", positions));
    }

    public async Task<bool> MightContainAsync(string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var positions = GetHashPositions(key);

        // 任务 2.2.1：使用 Lua 脚本一次 EVAL 替代 N 次串行 StringGetBitAsync 的 Task.WhenAll，
        // 将 N 次网络往返降为 1 次 EVAL，且保证原子读取（不被并发 SETBIT 中断导致中间状态）。
        // Lua 脚本遍历所有位偏移，全部为 1 返回 1（可能存在），任一为 0 返回 0（一定不存在）。
        var args = positions.Select(p => (RedisValue)p).ToArray();
        var result = await _database.ScriptEvaluateAsync(
            _batchGetBitsScript,
            new RedisKey[] { _redisKey },
            args).ConfigureAwait(false);

        return (long)result == 1;
    }

    private long[] GetHashPositions(string key)
    {
        var positions = new long[_hashCount];
        var keyBytes = Encoding.UTF8.GetBytes(key);

        // 使用双重哈希技术：h(i) = (hash1 + i * hash2) % m
        var hash1 = GetHash64(keyBytes, 0);
        var hash2 = GetHash64(keyBytes, 1);

        for (var i = 0; i < _hashCount; i++)
        {
            var combinedHash = unchecked(hash1 + (long)i * hash2);
            // 修复：Math.Abs(long.MinValue) 会溢出返回负数（long.MinValue），
            // 导致 positions[i] 为负数，Redis StringSetBitAsync 对负偏移量行为未定义。
            // 使用位掩码 & 0x7FFFFFFFFFFFFFFF 清除符号位，强制非负后再取模，
            // 保证结果落在 [0, _bitSize - 1] 范围内。
            var nonNegativeHash = combinedHash & 0x7FFFFFFFFFFFFFFF;
            positions[i] = nonNegativeHash % _bitSize;
        }

        return positions;
    }

    private static long GetHash64(byte[] data, int seed)
    {
        // 修复 T37：SHA256 是加密级哈希，对布隆过滤器非必要且性能开销大（~10x 慢于非加密哈希）。
        // 替换为 XxHash64（.NET 8+ 内置 System.IO.Hashing），非加密哈希，分布均匀，速度快。
        // 使用 seed 参数直接作为哈希种子，无需拼接 seedBytes，进一步减少分配。
        return (long)XxHash64.HashToUInt64(data, seed);
    }

    private static int CalculateBitSize(int expectedElements, double falsePositiveRate)
    {
        // m = -n * ln(p) / (ln(2)^2)
        var m = -(expectedElements * Math.Log(falsePositiveRate)) / (Math.Log(2) * Math.Log(2));
        return (int)Math.Ceiling(m);
    }

    private static int CalculateHashCount(int bitSize, int expectedElements)
    {
        // k = (m / n) * ln(2)
        var k = (bitSize / (double)expectedElements) * Math.Log(2);
        return Math.Max(1, (int)Math.Round(k));
    }
}