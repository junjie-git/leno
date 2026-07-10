using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 库存预占领域服务实现，协调多个 SKU 的批量预占/确认/释放操作。
/// 批量预占采用两阶段：逐个预占，任一失败则回滚已预占项。
/// </summary>
public sealed class StockReservationDomainService : IStockReservationDomainService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ILogger<StockReservationDomainService> _logger;

    public StockReservationDomainService(
        IInventoryRepository inventoryRepository,
        ILogger<StockReservationDomainService> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _inventoryRepository = inventoryRepository;
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
                // 回滚已预占的库存
                foreach (var (reservedSku, reservedQty) in reserved)
                {
                    await _inventoryRepository.ReleaseAsync(reservedSku, orderId, reservedQty, ct);
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
        foreach (var (skuId, quantity) in skuQuantities)
        {
            await _inventoryRepository.ReleaseAsync(skuId, orderId, quantity, ct);
        }
    }
}
