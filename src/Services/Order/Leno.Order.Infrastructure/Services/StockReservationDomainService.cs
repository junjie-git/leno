using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 库存预占领域服务实现，协调多个 SKU 的批量预占/确认/释放操作。
/// 批量预占采用两阶段：逐个预占，任一失败则回滚已预占项。
/// T18：回滚（<see cref="ReserveBatchAsync"/> 内部回滚或 <see cref="ReleaseBatchAsync"/> 调用）
/// 失败时将待释放 SKU 数量写入补偿表 <see cref="StockReservationCompensation"/>，
/// 由 <see cref="StockReservationCompensationBackgroundService"/> 定期重试，保证库存最终被释放。
/// </summary>
public sealed class StockReservationDomainService : IStockReservationDomainService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StockReservationDomainService> _logger;

    public StockReservationDomainService(
        IInventoryRepository inventoryRepository,
        IServiceScopeFactory scopeFactory,
        ILogger<StockReservationDomainService> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryRepository);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _inventoryRepository = inventoryRepository;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ReserveBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default)
    {
        // 记录已成功预占的 SKU，任一失败时按此回滚
        var reserved = new List<(Guid SkuId, int Quantity)>();

        foreach (var (skuId, quantity) in skuQuantities)
        {
            var success = await _inventoryRepository.ReserveAsync(skuId, orderId, quantity, ct);
            if (!success)
            {
                // 回滚已预占的库存（T18：回滚失败记入补偿表，由后台任务重试）
                foreach (var (reservedSku, reservedQty) in reserved)
                {
                    try
                    {
                        await _inventoryRepository.ReleaseAsync(reservedSku, orderId, reservedQty, ct);
                    }
                    catch (Exception ex)
                    {
                        await RecordCompensationAsync(orderId, reservedSku, reservedQty, ex,
                            CompensationOperationType.Release, ct);
                    }
                }

                _logger.LogWarning("批量预占失败，已回滚 {Count} 项 OrderId={OrderId} FailedSkuId={SkuId}",
                    reserved.Count, orderId, skuId);
                return false;
            }

            reserved.Add((skuId, quantity));
        }

        return true;
    }

    /// <inheritdoc />
    public async Task ConfirmBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default)
    {
        foreach (var (skuId, quantity) in skuQuantities)
        {
            await _inventoryRepository.ConfirmAsync(skuId, orderId, quantity, ct);
        }
    }

    /// <inheritdoc />
    public async Task ReleaseBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default)
    {
        // T18：逐个释放，单个 SKU 失败记入补偿表，不影响其它 SKU 释放
        foreach (var (skuId, quantity) in skuQuantities)
        {
            try
            {
                await _inventoryRepository.ReleaseAsync(skuId, orderId, quantity, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量释放库存失败，写入补偿表 OrderId={OrderId} SkuId={SkuId} Quantity={Quantity}",
                    orderId, skuId, quantity);
                await RecordCompensationAsync(orderId, skuId, quantity, ex,
                    CompensationOperationType.Release, ct);
            }
        }
    }

    /// <inheritdoc />
    public async Task ReturnDeductedBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default)
    {
        // 逐个归还已扣减库存，单个 SKU 失败记入补偿表，不影响其它 SKU 归还
        foreach (var (skuId, quantity) in skuQuantities)
        {
            try
            {
                await _inventoryRepository.ReturnDeductedAsync(skuId, orderId, quantity, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量归还已扣减库存失败，写入补偿表 OrderId={OrderId} SkuId={SkuId} Quantity={Quantity}",
                    orderId, skuId, quantity);
                await RecordCompensationAsync(orderId, skuId, quantity, ex,
                    CompensationOperationType.ReturnDeducted, ct);
            }
        }
    }

    /// <summary>
    /// 将回滚失败记录写入补偿表（独立 DbContext 作用域，避免污染 Saga 事务）。
    /// 补偿记录由 <see cref="StockReservationCompensationBackgroundService"/> 定期重试。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="skuId">待释放 SKU 标识。</param>
    /// <param name="quantity">待释放数量。</param>
    /// <param name="failureException">触发补偿的原始异常。</param>
    /// <param name="operationType">补偿操作类型，决定后台任务重试时调用 ReleaseAsync 还是 ReturnDeductedAsync（NEW-P0-3）。</param>
    /// <param name="ct">取消令牌。</param>
    private async Task RecordCompensationAsync(
        Guid orderId, Guid skuId, int quantity, Exception failureException,
        CompensationOperationType operationType, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IStockReservationCompensationRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var compensation = StockReservationCompensation.Create(
                Guid.NewGuid(), orderId, skuId, quantity, operationType: operationType);

            await repo.AddAsync(compensation, ct);
            await unitOfWork.SaveChangesAsync(ct);

            _logger.LogWarning("库存回滚失败已写入补偿表 OrderId={OrderId} SkuId={SkuId} Quantity={Quantity} OperationType={OperationType} Reason={Reason}",
                orderId, skuId, quantity, operationType, failureException.Message);
        }
        catch (Exception persistEx)
        {
            // 补偿记录持久化失败仅记日志，不阻塞后续回滚/补偿流程
            _logger.LogError(persistEx, "写入库存补偿记录失败 OrderId={OrderId} SkuId={SkuId} OriginalError={OriginalError}",
                orderId, skuId, failureException.Message);
        }
    }
}
