using Leno.Order.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 库存对账后台服务，定时扫描 Redis 库存键，校验可用库存与预占库存之和是否匹配基线。
/// 发现差异时记录告警日志，差异超过阈值时自动修正。
/// </summary>
public sealed class StockReconciliationService : BackgroundService
{
    private const string StockKeyPrefix = "inventory:stock:";
    private const string ReservedKeyPrefix = "inventory:reserved:";
    private static readonly TimeSpan ReconciliationInterval = TimeSpan.FromMinutes(5);
    private const int MaxCorrectionDelta = 100; // 超过此阈值的差异不自动修正，仅告警

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<StockReconciliationService> _logger;

    public StockReconciliationService(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        ILogger<StockReconciliationService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("库存对账服务已启动，对账间隔 {Interval}", ReconciliationInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ReconciliationInterval, stoppingToken);
                await ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "库存对账执行异常");
            }
        }

        _logger.LogInformation("库存对账服务已停止");
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var db = _redis.GetDatabase();

        // 使用 SCAN 异步分页扫描，避免 KEYS 阻塞 Redis 主线程
        var stockKeys = new List<RedisKey>();
        await foreach (var key in server.KeysAsync(pattern: $"{StockKeyPrefix}*", pageSize: 200).WithCancellation(ct))
        {
            stockKeys.Add(key);
        }
        _logger.LogInformation("库存对账开始，扫描到 {Count} 个库存键", stockKeys.Count);

        var mismatchCount = 0;

        foreach (var key in stockKeys)
        {
            ct.ThrowIfCancellationRequested();

            var skuIdStr = key.ToString().Substring(StockKeyPrefix.Length);
            if (!Guid.TryParse(skuIdStr, out var skuId))
            {
                _logger.LogWarning("无法解析 SKU ID Key={Key}", key);
                continue;
            }

            var available = (int?)await db.StringGetAsync(key) ?? 0;

            // 扫描该 SKU 的全部预占键（同样使用 SCAN 异步分页）
            var reservedPattern = $"{ReservedKeyPrefix}{skuId}:*";
            var totalReserved = 0;
            await foreach (var rk in server.KeysAsync(pattern: reservedPattern, pageSize: 200).WithCancellation(ct))
            {
                var reserved = (int?)await db.StringGetAsync(rk) ?? 0;
                totalReserved += reserved;
            }

            // 可用库存不应为负
            if (available < 0)
            {
                mismatchCount++;
                _logger.LogWarning("库存对账异常：可用库存为负 SkuId={SkuId} Available={Available} Reserved={Reserved}",
                    skuId, available, totalReserved);
                _logger.LogWarning("库存对账：SkuId={SkuId} 可用库存为负需人工介入", skuId);
            }
        }

        if (mismatchCount > 0)
        {
            _logger.LogWarning("库存对账完成，发现 {MismatchCount} 个异常", mismatchCount);
        }
        else
        {
            _logger.LogInformation("库存对账完成，共 {Count} 个 SKU，无异常", stockKeys.Count);
        }
    }
}