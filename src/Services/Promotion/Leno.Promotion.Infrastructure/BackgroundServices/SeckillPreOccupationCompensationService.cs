using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.BackgroundServices;

/// <summary>
/// 秒杀预占补偿后台服务，定时扫描超时未履约的预占记录并回退库存。
/// 默认每 30 秒扫描一次，超时阈值 5 分钟。
/// </summary>
public sealed class SeckillPreOccupationCompensationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeckillPreOccupationCompensationService> _logger;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TimeoutThreshold = TimeSpan.FromMinutes(5);
    private const int BatchSize = 100;

    public SeckillPreOccupationCompensationService(
        IServiceScopeFactory scopeFactory,
        ILogger<SeckillPreOccupationCompensationService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("秒杀预占补偿服务启动，扫描间隔 {Interval}s，超时阈值 {Timeout}min",
            ScanInterval.TotalSeconds, TimeoutThreshold.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CompensateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "秒杀预占补偿执行异常");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }

        _logger.LogInformation("秒杀预占补偿服务停止");
    }

    private async Task CompensateAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var recordRepository = scope.ServiceProvider.GetRequiredService<ISeckillPreOccupationRecordRepository>();
        var activityRepository = scope.ServiceProvider.GetRequiredService<ISeckillActivityRepository>();
        var stockService = scope.ServiceProvider.GetRequiredService<ISeckillStockService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var timeout = DateTime.UtcNow - TimeoutThreshold;
        var records = await recordRepository.GetUnfulfilledAsync(timeout, 0, BatchSize, ct);

        if (records.Count == 0)
        {
            return;
        }

        _logger.LogInformation("扫描到 {Count} 条超时未履约预占记录，开始补偿", records.Count);

        foreach (var record in records)
        {
            try
            {
                // 回退 Redis 库存
                await stockService.RestoreAsync(record.ActivityId, record.SkuId, record.Quantity, ct);

                // 回退 DB 基线库存
                var activity = await activityRepository.GetByIdAsync(record.ActivityId, ct);
                if (activity is not null)
                {
                    activity.RestoreStock(record.Quantity);
                }

                record.MarkRolledBack();
                await unitOfWork.SaveEntitiesAsync(ct);

                _logger.LogInformation(
                    "补偿回退完成 OrderId={OrderId} ActivityId={ActivityId} SkuId={SkuId} Quantity={Quantity}",
                    record.OrderId, record.ActivityId, record.SkuId, record.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "补偿回退失败 OrderId={OrderId}", record.OrderId);
            }
        }
    }
}