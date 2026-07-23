using Leno.Inventory.Application.DTOs;
using Leno.SharedContracts.Integration.Inventory;

namespace Leno.Inventory.Application;

/// <summary>
/// 库存应用服务接口，暴露给 Order BC 经集成命令或进程内调用。
/// 所有方法均幂等：基于 IdempotencyKey 去重。
/// 双轨期：flag=true 时 Order BC 经 MassTransit 发布命令由 Consumers 调用本接口；
/// flag=false 时 Order BC 通过进程内 IInventoryAppService 直接调用（此时 Order BC 自行从订单明细构建 items）。
/// </summary>
public interface IInventoryAppService
{
    /// <summary>
    /// 预占库存（下单），按 SKU 维度批量预占，任一 SKU 失败回滚已预占项并返回失败。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="items">预占明细（SkuId/Quantity/SellerId）。</param>
    /// <param name="idempotencyKey">幂等键，相同键重复调用返回首次结果。</param>
    /// <param name="ct">取消令牌。</param>
    Task<StockReservationResult> ReserveAsync(
        Guid orderId,
        IReadOnlyList<ReserveStockItem> items,
        Guid idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// 确认扣减库存（支付成功），将订单预占转为真实扣减。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="items">确认明细（SkuId/Quantity/SellerId）。</param>
    /// <param name="idempotencyKey">幂等键。</param>
    /// <param name="ct">取消令牌。</param>
    Task ConfirmAsync(
        Guid orderId,
        IReadOnlyList<ReserveStockItem> items,
        Guid idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// 释放预占库存（订单取消未支付）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="items">释放明细（SkuId/Quantity/SellerId）。</param>
    /// <param name="idempotencyKey">幂等键。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReleaseAsync(
        Guid orderId,
        IReadOnlyList<ReserveStockItem> items,
        Guid idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// 归还已扣减库存（已支付/已发货订单强制取消）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="items">归还明细（SkuId/Quantity/SellerId）。</param>
    /// <param name="idempotencyKey">幂等键。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReturnDeductedAsync(
        Guid orderId,
        IReadOnlyList<ReserveStockItem> items,
        Guid idempotencyKey,
        CancellationToken ct = default);
}
