using System.Security.Cryptography;
using System.Text;
using Leno.SharedKernel.Abstractions;
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

        var tasks = new List<Task<bool>>(positions.Length);

        foreach (var position in positions)
        {
            tasks.Add(_database.StringSetBitAsync(_redisKey, position, true));
        }

        await Task.WhenAll(tasks);

        _logger.LogDebug("BloomFilter 添加 key: {Key}, Positions: {Positions}", key, string.Join(",", positions));
    }

    public async Task<bool> MightContainAsync(string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var positions = GetHashPositions(key);

        var tasks = new List<Task<bool>>(positions.Length);

        foreach (var position in positions)
        {
            tasks.Add(_database.StringGetBitAsync(_redisKey, position));
        }

        var results = await Task.WhenAll(tasks);

        return results.All(r => r);
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
            positions[i] = Math.Abs(combinedHash % _bitSize);
        }

        return positions;
    }

    private static long GetHash64(byte[] data, int seed)
    {
        // 使用 SHA256 结合种子生成 64 位哈希
        var seedBytes = BitConverter.GetBytes(seed);
        var input = new byte[data.Length + seedBytes.Length];
        Buffer.BlockCopy(data, 0, input, 0, data.Length);
        Buffer.BlockCopy(seedBytes, 0, input, data.Length, seedBytes.Length);

        var hash = SHA256.HashData(input);
        return BitConverter.ToInt64(hash, 0);
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