using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.BackgroundServices;

/// <summary>
/// 秒杀预占补偿后台服务，定时扫描超时未履约的预占记录并回退库存。
/// 默认每 10 秒扫描一次（与事务内重校验配套，避免大批量下误回退），超时阈值 5 分钟。
/// 批量大小 500，原 100+30s 在 1000 条积压下需 5 分钟清完，期间用户订单可能已确认但补偿仍误回退。
/// </summary>
public sealed class SeckillPreOccupationCompensationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeckillPreOccupationCompensationService> _logger;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TimeoutThreshold = TimeSpan.FromMinutes(5);
    private const int BatchSize = 500;

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
                await using var tx = await unitOfWork.BeginTransactionAsync(ct);

                // 事务内重新加载记录，校验状态是否在读取后被变更（防 TOCTOU 竞态）：
                // 若 SeckillOrderConfirmedEventConsumer 已在读取后置 IsFulfilled=true，
                // 补偿不应继续回退库存，否则产生 IsFulfilled=true && IsRolledBack=true 非法状态
                var fresh = await recordRepository.GetByIdAsync(record.Id, ct);
                if (fresh is null || fresh.IsFulfilled || fresh.IsRolledBack)
                {
                    _logger.LogInformation(
                        "记录已变更 OrderId={OrderId} IsFulfilled={IsFulfilled} IsRolledBack={IsRolledBack}，跳过补偿",
                        record.OrderId, fresh?.IsFulfilled ?? false, fresh?.IsRolledBack ?? false);
                    continue;
                }

                // 回退 Redis 库存
                await stockService.RestoreAsync(fresh.ActivityId, fresh.SkuId, fresh.Quantity, ct);

                // 回退 DB 基线库存
                var activity = await activityRepository.GetByIdAsync(fresh.ActivityId, ct);
                if (activity is not null)
                {
                    activity.RestoreStock(fresh.Quantity);
                }

                fresh.MarkRolledBack();
                await unitOfWork.SaveEntitiesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "补偿回退完成 OrderId={OrderId} ActivityId={ActivityId} SkuId={SkuId} Quantity={Quantity}",
                    fresh.OrderId, fresh.ActivityId, fresh.SkuId, fresh.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "补偿回退失败 OrderId={OrderId}", record.OrderId);
            }
        }
    }
}