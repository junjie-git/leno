using Leno.Infrastructure.Abstractions;
using Leno.Inventory.Application.DTOs;
using Leno.Inventory.Domain.Aggregates;
using Leno.Inventory.Domain.Exceptions;
using Leno.Inventory.Domain.Repositories;
using Leno.SharedContracts.Integration.Inventory;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Application.Services;

/// <summary>
/// 库存应用服务实现 —— 预占 / 确认 / 释放 / 归还四个用例。
/// <para>
/// 一致性模型：每个用例在**同一数据库事务**内完成"台账写入/迁移 + 基线原子条件 UPDATE"，
/// 任一步失败整体回滚（无半成功状态，因此无需自建补偿机器 —— 失败交给调用方或消息重试）。
/// 幂等三层：① 幂等存储（Redis，快速去重）；② 台账唯一约束 (order_id, sku_id)（数据库硬保证）；
/// ③ 单向状态机（终态重复命令为 no-op）。
/// </para>
/// <para>
/// 双轨下线 DEC-4（2026-09-22）：旧的"Redis 原子层 + DB 聚合审计双写 + 对账/补偿兜底"
/// 模式已废除 —— 库存以 Inventory BC 的 SQL 为唯一权威，Redis 仅保留秒杀配额通道。
/// </para>
/// </summary>
public sealed class InventoryAppService : IInventoryAppService
{
    private readonly IStockReservationRepository _reservationRepository;
    private readonly IStockBaselineRepository _baselineRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<InventoryAppService> _logger;

    public InventoryAppService(
        IStockReservationRepository reservationRepository,
        IStockBaselineRepository baselineRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IIdempotencyStore idempotencyStore,
        ILogger<InventoryAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(reservationRepository);
        ArgumentNullException.ThrowIfNull(baselineRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(publishEndpoint);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(logger);
        _reservationRepository = reservationRepository;
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
        if (items.Any(i => i.Quantity <= 0))
        {
            return StockReservationResult.Failed("预占数量须大于 0");
        }

        // 幂等第一层：幂等存储快速去重
        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, ct).ConfigureAwait(false))
        {
            _logger.LogInformation("库存预占已处理，幂等重放 OrderId={OrderId} Key={Key}", orderId, idempotencyKey);
            return SucceededFrom(items);
        }

        // 幂等第二层：台账唯一约束 —— 该订单已预占（含第一层存储丢失的场景，如 Redis 重启）
        if (await _reservationRepository.ExistsForOrderAsync(orderId, ct).ConfigureAwait(false))
        {
            _logger.LogWarning("订单已存在库存台账，按幂等重放处理 OrderId={OrderId}", orderId);
            return SucceededFrom(items);
        }

        var transaction = await _unitOfWork.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            // 1. 台账写入（Reserved 态；唯一约束 (order_id, sku_id) 在提交时兜底并发）
            var entries = items
                .Select(i => StockReservation.CreateReserved(
                    Guid.NewGuid(), orderId, i.SkuId, i.Quantity, idempotencyKey))
                .ToList();
            await _reservationRepository.AddRangeAsync(entries, ct).ConfigureAwait(false);

            // 2. 基线原子预占：任一 SKU 不足即抛出 → 整个事务回滚（无半成功）
            foreach (var item in items)
            {
                var affected = await _baselineRepository.TryReserveAsync(item.SkuId, item.Quantity, ct)
                    .ConfigureAwait(false);
                if (affected == 0)
                {
                    throw new InventoryDomainException(
                        $"SKU {item.SkuId} 库存不足", "STOCK_INSUFFICIENT");
                }
            }

            await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch (InventoryDomainException ex) when (ex.ErrorCode == "STOCK_INSUFFICIENT")
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogWarning("库存预占失败（事务已回滚）OrderId={OrderId} Reason={Reason}", orderId, ex.Message);
            return StockReservationResult.Failed(ex.Message);
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct).ConfigureAwait(false);

        var reservedItems = items
            .Select(i => new ReservedSkuItem(i.SkuId, i.Quantity))
            .ToList();
        // 预占过期由 Order BC 的订单超时（30 分钟）驱动 ReleaseStockCommand，Inventory 侧不另设 TTL
        await _publishEndpoint.Publish(
            new StockReservedIntegrationEvent(orderId, reservedItems, expiresAt: null), ct);

        _logger.LogInformation("库存预占成功 OrderId={OrderId} ItemCount={Count}", orderId, reservedItems.Count);
        return StockReservationResult.Succeeded(reservedItems, expiresAt: null);
    }

    /// <inheritdoc />
    public async Task ConfirmAsync(Guid orderId, Guid idempotencyKey, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId 不可为空", nameof(orderId));
        }

        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, ct).ConfigureAwait(false))
        {
            _logger.LogInformation("库存确认已处理，幂等重放 OrderId={OrderId}", orderId);
            return;
        }

        var transaction = await _unitOfWork.BeginTransactionAsync(ct).ConfigureAwait(false);
        var confirmedCount = 0;
        try
        {
            var entries = await _reservationRepository.GetByOrderAsync(orderId, ct).ConfigureAwait(false);
            if (entries.Count == 0)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("库存确认：订单无台账条目，跳过 OrderId={OrderId}", orderId);
                return;
            }

            var reserved = entries
                .Where(e => e.Status == StockReservationStatus.Reserved)
                .ToList();
            if (reserved.Count == 0)
            {
                // 全部已处于终态（含 Confirmed）→ 幂等 no-op
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct).ConfigureAwait(false);
                _logger.LogInformation("库存确认：订单条目均已终态，幂等跳过 OrderId={OrderId}", orderId);
                return;
            }

            foreach (var entry in reserved)
            {
                var affected = await _baselineRepository.TryConfirmAsync(entry.SkuId, entry.Quantity, ct)
                    .ConfigureAwait(false);
                if (affected == 0)
                {
                    // 台账与基线由同事务维护，此处不一致即数据异常 —— 显式失败并回滚，禁止静默吞掉
                    throw new InventoryDomainException(
                        $"SKU {entry.SkuId} 基线预占计数与台账不一致", "STOCK_BASELINE_LEDGER_MISMATCH");
                }

                entry.MarkConfirmed();
            }

            confirmedCount = reserved.Count;
            await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct).ConfigureAwait(false);
        await _publishEndpoint.Publish(new StockConfirmedIntegrationEvent(orderId), ct);

        _logger.LogInformation("库存确认完成 OrderId={OrderId} ConfirmedCount={Count}", orderId, confirmedCount);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(
        Guid orderId,
        ReleaseStockOperationType operationType,
        Guid idempotencyKey,
        CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId 不可为空", nameof(orderId));
        }

        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, ct).ConfigureAwait(false))
        {
            _logger.LogInformation("库存释放已处理，幂等重放 OrderId={OrderId} OpType={OpType}", orderId, operationType);
            return;
        }

        var transaction = await _unitOfWork.BeginTransactionAsync(ct).ConfigureAwait(false);
        var releasedCount = 0;
        try
        {
            var entries = await _reservationRepository.GetByOrderAsync(orderId, ct).ConfigureAwait(false);
            if (entries.Count == 0)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("库存释放：订单无台账条目，跳过 OrderId={OrderId} OpType={OpType}",
                    orderId, operationType);
                return;
            }

            // Release 作用于 Reserved 态；ReturnDeducted 作用于 Confirmed 态
            var targets = entries
                .Where(e => operationType == ReleaseStockOperationType.Release
                    ? e.Status == StockReservationStatus.Reserved
                    : e.Status == StockReservationStatus.Confirmed)
                .ToList();
            if (targets.Count == 0)
            {
                // 无可操作条目（已释放/已归还/状态不符）→ 幂等 no-op
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct).ConfigureAwait(false);
                _logger.LogInformation("库存释放：无可操作条目，幂等跳过 OrderId={OrderId} OpType={OpType}",
                    orderId, operationType);
                return;
            }

            foreach (var entry in targets)
            {
                var affected = operationType == ReleaseStockOperationType.Release
                    ? await _baselineRepository.TryReleaseAsync(entry.SkuId, entry.Quantity, ct).ConfigureAwait(false)
                    : await _baselineRepository.TryReturnAsync(entry.SkuId, entry.Quantity, ct).ConfigureAwait(false);
                if (affected == 0)
                {
                    throw new InventoryDomainException(
                        $"SKU {entry.SkuId} 基线计数与台账不一致", "STOCK_BASELINE_LEDGER_MISMATCH");
                }

                if (operationType == ReleaseStockOperationType.Release)
                {
                    entry.MarkReleased();
                }
                else
                {
                    entry.MarkReturned();
                }
            }

            releasedCount = targets.Count;
            await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, ct).ConfigureAwait(false);
        await _publishEndpoint.Publish(new StockReleasedIntegrationEvent(orderId, operationType), ct);

        _logger.LogInformation("库存释放完成 OrderId={OrderId} OpType={OpType} ReleasedCount={Count}",
            orderId, operationType, releasedCount);
    }

    /// <inheritdoc />
    public Task<int> GetSellableAsync(Guid skuId, CancellationToken ct = default)
        => _baselineRepository.GetSellableAsync(skuId, ct);

    private static StockReservationResult SucceededFrom(IReadOnlyList<ReserveStockItem> items)
        => StockReservationResult.Succeeded(
            items.Select(i => new ReservedSkuItem(i.SkuId, i.Quantity)).ToList(),
            expiresAt: null);
}
