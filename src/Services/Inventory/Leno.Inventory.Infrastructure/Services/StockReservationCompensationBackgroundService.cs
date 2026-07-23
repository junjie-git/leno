using Leno.Inventory.Domain.Aggregates;
using Leno.Inventory.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Inventory.Infrastructure.Services;

/// <summary>
/// 库存预占回滚补偿后台服务配置（T18）。
/// 通过 <c>appsettings.json</c> 的 <c>StockReservationCompensation</c> 节绑定。
/// 迁移自 Order BC。
/// </summary>
public sealed class StockReservationCompensationOptions
{
    /// <summary>重试执行间隔，默认 5 分钟。</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>每批拉取的 Pending 补偿记录数量，默认 50。</summary>
    public int BatchSize { get; set; } = 50;
}

/// <summary>
/// 库存预占回滚补偿后台服务（T18），迁移自 Order BC。
/// 定期拉取 <see cref="StockReservationCompensation"/> 表中 <see cref="CompensationStatus.Pending"/> 记录，
/// 按每条记录的 <see cref="StockReservationCompensation.OperationType"/> 分发到对应库存仓储方法（NEW-P0-3）：
/// <list type="bullet">
/// <item><see cref="CompensationOperationType.Release"/> → <see cref="IInventoryRepository.ReleaseAsync"/>（释放预占）</item>
/// <item><see cref="CompensationOperationType.ReturnDeducted"/> → <see cref="IInventoryRepository.ReturnDeductedAsync"/>（归还已扣减）</item>
/// </list>
/// - 成功：<see cref="StockReservationCompensation.MarkSucceeded"/>
/// - 失败：<see cref="StockReservationCompensation.MarkFailed"/>（达到 <see cref="StockReservationCompensation.MaxRetries"/> 自动流转到 <see cref="CompensationStatus.MaxRetriesExceeded"/> 等待人工介入）
/// 每条记录独立事务提交，单条失败不影响其它记录重试。
/// </summary>
public sealed class StockReservationCompensationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StockReservationCompensationBackgroundService> _logger;
    private readonly StockReservationCompensationOptions _options;

    public StockReservationCompensationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<StockReservationCompensationBackgroundService> logger,
        IOptions<StockReservationCompensationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("库存预占回滚补偿服务已启动，重试间隔 {Interval}", _options.Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.Interval, stoppingToken);
                await RunRetryCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "库存预占回滚补偿服务执行异常");
            }
        }

        _logger.LogInformation("库存预占回滚补偿服务已停止");
    }

    /// <summary>
    /// 执行一轮补偿重试：拉取 Pending 记录并按 <see cref="StockReservationCompensation.OperationType"/>
    /// 分发到对应库存仓储方法（释放预占或归还已扣减）。
    /// 每条记录独立提交，单条失败不影响其它记录。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task RunRetryCycleAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var compensationRepo = scope.ServiceProvider.GetRequiredService<IStockReservationCompensationRepository>();
        var inventoryRepo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var batchSize = _options.BatchSize > 0 ? _options.BatchSize : 50;
        var pending = await compensationRepo.GetPendingAsync(batchSize, ct);

        if (pending.Count == 0)
        {
            return;
        }

        var succeeded = 0;
        var failed = 0;
        var exhausted = 0;

        foreach (var compensation in pending)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // 按 OperationType 分发到对应库存仓储方法（NEW-P0-3）：
                // Release → ReleaseAsync（释放预占），ReturnDeducted → ReturnDeductedAsync（归还已扣减）
                switch (compensation.OperationType)
                {
                    case CompensationOperationType.Release:
                        await inventoryRepo.ReleaseAsync(compensation.SkuId, compensation.OrderId, compensation.Quantity, ct);
                        break;
                    case CompensationOperationType.ReturnDeducted:
                        await inventoryRepo.ReturnDeductedAsync(compensation.SkuId, compensation.OrderId, compensation.Quantity, ct);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"未知的补偿操作类型 {compensation.OperationType}，CompensationId={compensation.Id}");
                }

                compensation.MarkSucceeded();
                await compensationRepo.UpdateAsync(compensation, ct);
                await unitOfWork.SaveChangesAsync(ct);
                succeeded++;
            }
            catch (Exception ex)
            {
                compensation.MarkFailed(ex.Message);
                try
                {
                    await compensationRepo.UpdateAsync(compensation, ct);
                    await unitOfWork.SaveChangesAsync(ct);
                }
                catch (Exception persistEx)
                {
                    // 状态持久化失败仅记日志，不阻塞其它记录重试；下次轮询仍会拉取到该 Pending 记录
                    _logger.LogError(persistEx, "库存补偿记录状态持久化失败 CompensationId={CompensationId}", compensation.Id);
                }

                if (compensation.Status == CompensationStatus.MaxRetriesExceeded)
                {
                    exhausted++;
                    _logger.LogWarning("库存补偿记录已达最大重试次数，等待人工介入 CompensationId={CompensationId} OrderId={OrderId} SkuId={SkuId} Quantity={Quantity}",
                        compensation.Id, compensation.OrderId, compensation.SkuId, compensation.Quantity);
                }
                else
                {
                    failed++;
                    _logger.LogWarning(ex, "库存补偿重试失败 CompensationId={CompensationId} OrderId={OrderId} SkuId={SkuId} RetryCount={RetryCount}",
                        compensation.Id, compensation.OrderId, compensation.SkuId, compensation.RetryCount);
                }
            }
        }

        _logger.LogInformation("库存补偿重试周期完成，共处理 {Total} 条：成功 {Succeeded}，失败 {Failed}，达上限 {Exhausted}",
            pending.Count, succeeded, failed, exhausted);
    }
}
