using Leno.Order.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 库存 Redis-DB 对账后台服务配置。
/// 通过 <c>appsettings.json</c> 的 <c>InventoryReconciliation</c> 节绑定。
/// </summary>
public sealed class InventoryReconciliationOptions
{
    /// <summary>对账执行间隔，默认 1 小时。</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>每批从 DB 加载的 StockReservation 数量，默认 100。</summary>
    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// 库存 Redis-DB 对账后台服务。
/// 定期从 DB 查询 <see cref="StockReservation"/> 聚合的可用库存（<see cref="StockReservation.AvailableQty"/>），
/// 与 Redis 中 <c>inventory:stock:{skuId}</c> 的值比较。
/// 不一致时记录告警日志，并以 DB 为准刷新 Redis（DB 是事务真相源，避免 Redis 故障/脚本错误导致超卖）。
/// </summary>
/// <remarks>
/// 需在 Order 表现层 Program.cs 注册：
/// <c>services.AddHostedService&lt;InventoryReconciliationBackgroundService&gt;();</c>
/// 并可选绑定配置：<c>services.Configure&lt;InventoryReconciliationOptions&gt;(configuration.GetSection("InventoryReconciliation"));</c>
/// </remarks>
public sealed class InventoryReconciliationBackgroundService : BackgroundService
{
    private const string StockKeyPrefix = "inventory:stock:";

    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<InventoryReconciliationBackgroundService> _logger;
    private readonly InventoryReconciliationOptions _options;

    public InventoryReconciliationBackgroundService(
        IServiceProvider serviceProvider,
        IConnectionMultiplexer redis,
        ILogger<InventoryReconciliationBackgroundService> logger,
        IOptions<InventoryReconciliationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        _serviceProvider = serviceProvider;
        _redis = redis;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("库存 Redis-DB 对账服务已启动，对账间隔 {Interval}", _options.Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.Interval, stoppingToken);
                await RunReconciliationCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "库存 Redis-DB 对账执行异常");
            }
        }

        _logger.LogInformation("库存 Redis-DB 对账服务已停止");
    }

    private async Task RunReconciliationCycleAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var batchSize = _options.BatchSize > 0 ? _options.BatchSize : 100;
        var skip = 0;
        var totalScanned = 0;
        var totalMismatch = 0;

        while (!ct.IsCancellationRequested)
        {
            var page = await dbContext.StockReservations
                .OrderBy(r => r.Id)
                .Skip(skip)
                .Take(batchSize)
                .ToListAsync(ct);

            if (page.Count == 0)
            {
                break;
            }

            totalScanned += page.Count;
            totalMismatch += await ReconcileAsync(page, ct);

            skip += page.Count;
            if (page.Count < batchSize)
            {
                break;
            }
        }

        if (totalMismatch > 0)
        {
            _logger.LogWarning("库存 Redis-DB 对账周期完成，扫描 {Scanned} 个 SKU，发现 {Mismatch} 个不一致并已刷新 Redis",
                totalScanned, totalMismatch);
        }
        else
        {
            _logger.LogInformation("库存 Redis-DB 对账周期完成，扫描 {Scanned} 个 SKU，无异常", totalScanned);
        }
    }

    /// <summary>
    /// 对一批 <see cref="StockReservation"/> 与 Redis 执行对账。
    /// 不一致时记录告警并以 DB 为准刷新 Redis，返回不一致并刷新的 SKU 数量。
    /// </summary>
    /// <param name="reservations">待对账的库存预占聚合列表。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>本轮不一致并刷新 Redis 的 SKU 数量。</returns>
    public async Task<int> ReconcileAsync(IReadOnlyList<StockReservation> reservations, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reservations);
        var db = _redis.GetDatabase();
        var mismatchCount = 0;

        foreach (var reservation in reservations)
        {
            ct.ThrowIfCancellationRequested();

            var stockKey = $"{StockKeyPrefix}{reservation.SkuId}";
            var redisValue = (int?)await db.StringGetAsync(stockKey) ?? 0;
            var dbValue = reservation.AvailableQty;

            if (redisValue != dbValue)
            {
                mismatchCount++;
                _logger.LogWarning(
                    "库存对账不一致：SkuId={SkuId} Redis={Redis} DB={Db}，以 DB 为准刷新 Redis",
                    reservation.SkuId, redisValue, dbValue);

                await db.StringSetAsync(stockKey, dbValue);
            }
        }

        return mismatchCount;
    }
}
