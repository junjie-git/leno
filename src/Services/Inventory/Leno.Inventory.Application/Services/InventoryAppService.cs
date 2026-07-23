using Leno.Infrastructure.Abstractions;
using Leno.Inventory.Application.DTOs;
using Leno.Inventory.Domain.Repositories;
using Leno.SharedContracts.Integration.Inventory;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Application.Services;

/// <summary>
/// 库存应用服务实现，封装预占/确认/释放/归还四个核心用例。
/// 双轨期：当 Inventory:UseExternalBc=true 时，Order BC 经 MassTransit 发布
/// <see cref="ReserveStockCommand"/> 由本服务的消费者调用此类；
/// flag=false 时，Order BC 通过进程内 IInventoryAppService 直接调用（此时 Order BC 自行从订单明细构建 items）。
/// 操作经 <see cref="IInventoryRepository"/>（Redis 原子层 + DB 聚合双写）执行，
/// 成功后发布对应集成事件供 Order Saga 推进状态机。
/// </summary>
public sealed class InventoryAppService : IInventoryAppService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockBaselineRepository _baselineRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<InventoryAppService> _logger;

    public InventoryAppService(
        IInventoryRepository inventoryRepository,
        IStockBaselineRepository baselineRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IIdempotencyStore idempotencyStore,
        ILogger<InventoryAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryRepository);
        ArgumentNullException.ThrowIfNull(baselineRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(publishEndpoint);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(logger);
        _inventoryRepository = inventoryRepository;
        _baselineRepository = baselineRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StockReservationResult> ReserveAsync(
        Guid orderId,
        IReadOnlyList<ReserveStockItem> items,
        Guid idempotencyKey,
        CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId 不可为空", nameof(orderId));
        }
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return StockReservationResult.Failed("预占明细为空");
        }

        // 幂等：基于 IdempotencyKey 去重，已处理则跳过
        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, ct))
        {
            _logger.LogInformation("库存预占已处理，跳过重复调用 OrderId={OrderId} IdempotencyKey={Key}",
                orderId, idempotencyKey);
            return StockReservationResult.Succeeded(
                items.Select(i => new ReservedSkuItem(i.SkuId, i.Quantity, i.SellerId)).ToList(),
                expiresAt: null);
        }

        var reservedItems = new List<ReservedSkuItem>(items.Count);
        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                return StockReservationResult.Failed($"SKU {item.SkuId} 预占数量须大于 0");
            }

            var success = await _inventoryRepository.ReserveAsync(item.SkuId, orderId, item.Quantity, ct);
            if (!success)
            {
                // 任一 SKU 失败：回滚已预占项
                await RollbackReservedAsync(orderId, reservedItems, ct);
                _logger.LogWarning("库存预占失败，已回滚 OrderId={OrderId} FailedSkuId={SkuId}", orderId, item.SkuId);
                return StockReservationResult.Failed($"SKU {item.SkuId} 库存不足");
            }

            reservedItems.Add(new ReservedSkuItem(item.SkuId, item.Quantity, item.SellerId));
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct);

        DateTime? expiresAt = null;
        await _publishEndpoint.Publish(
            new StockReservedIntegrationEvent(orderId, reservedItems, expiresAt), ct);

        _logger.LogInformation("库存预占成功 OrderId={OrderId} ItemCount={Count}", orderId, reservedItems.Count);
        return StockReservationResult.Succeeded(reservedItems, expiresAt);
    }

    /// <inheritdoc />
    public async Task ConfirmAsync(
        Guid orderId,
        IReadOnlyList<ReserveStockItem> items,
        Guid idempotencyKey,
        CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId 不可为空", nameof(orderId));
        }
        ArgumentNullException.ThrowIfNull(items);

        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, ct))
        {
            _logger.LogInformation("库存确认已处理，跳过重复调用 OrderId={OrderId}", orderId);
            return;
        }

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                continue;
            }
            await _inventoryRepository.ConfirmAsync(item.SkuId, orderId, item.Quantity, ct);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct);
        await _publishEndpoint.Publish(new StockConfirmedIntegrationEvent(orderId), ct);

        _logger.LogInformation("库存确认完成 OrderId={OrderId} ItemCount={Count}", orderId, items.Count);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(
        Guid orderId,
        IReadOnlyList<ReserveStockItem> items,
        Guid idempotencyKey,
        CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId 不可为空", nameof(orderId));
        }
        ArgumentNullException.ThrowIfNull(items);

        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, ct))
        {
            _logger.LogInformation("库存释放已处理，跳过重复调用 OrderId={OrderId}", orderId);
            return;
        }

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                continue;
            }
            await _inventoryRepository.ReleaseAsync(item.SkuId, orderId, item.Quantity, ct);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct);
        await _publishEndpoint.Publish(
            new StockReleasedIntegrationEvent(orderId, ReleaseStockOperationType.Release), ct);

        _logger.LogInformation("库存预占释放完成 OrderId={OrderId} ItemCount={Count}", orderId, items.Count);
    }

    /// <inheritdoc />
    public async Task ReturnDeductedAsync(
        Guid orderId,
        IReadOnlyList<ReserveStockItem> items,
        Guid idempotencyKey,
        CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId 不可为空", nameof(orderId));
        }
        ArgumentNullException.ThrowIfNull(items);

        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, ct))
        {
            _logger.LogInformation("库存归还已处理，跳过重复调用 OrderId={OrderId}", orderId);
            return;
        }

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                continue;
            }
            await _inventoryRepository.ReturnDeductedAsync(item.SkuId, orderId, item.Quantity, ct);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);
        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct);
        await _publishEndpoint.Publish(
            new StockReleasedIntegrationEvent(orderId, ReleaseStockOperationType.ReturnDeducted), ct);

        _logger.LogInformation("库存归还完成 OrderId={OrderId} ItemCount={Count}", orderId, items.Count);
    }

    /// <summary>
    /// 回滚已预占项（任一 SKU 失败时调用）。
    /// 单个释放失败仅记日志，由 <c>StockReservationCompensationBackgroundService</c> 兜底重试。
    /// </summary>
    private async Task RollbackReservedAsync(
        Guid orderId,
        IReadOnlyList<ReservedSkuItem> reserved,
        CancellationToken ct)
    {
        foreach (var item in reserved)
        {
            try
            {
                await _inventoryRepository.ReleaseAsync(item.SkuId, orderId, item.Quantity, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "回滚预占失败，等待补偿任务兜底 OrderId={OrderId} SkuId={SkuId} Quantity={Quantity}",
                    orderId, item.SkuId, item.Quantity);
            }
        }
    }
}
