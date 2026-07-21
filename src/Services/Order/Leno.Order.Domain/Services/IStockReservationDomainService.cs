namespace Leno.Order.Domain.Services;

/// <summary>
/// 库存预占领域服务接口，封装跨多 SKU 的批量预占/确认/释放操作。
/// 实现位于基础设施层，协调多个 <see cref="Aggregates.StockReservation"/> 聚合，
/// 保证批量操作的原子性（任一 SKU 失败则整体回滚）。
/// </summary>
public interface IStockReservationDomainService
{
    /// <summary>
    /// 批量预占库存（下单），全部 SKU 预占成功返回 true，任一失败回滚并返回 false。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="skuQuantities">SKU 与预占数量映射。</param>
    /// <param name="ct">取消令牌。</param>
    Task<bool> ReserveBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default);

    /// <summary>
    /// 批量确认扣减库存（支付成功），将预占转为真实扣减。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="skuQuantities">SKU 与确认数量映射。</param>
    /// <param name="ct">取消令牌。</param>
    Task ConfirmBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default);

    /// <summary>
    /// 批量释放预占库存（订单取消），回退预占数量。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="skuQuantities">SKU 与释放数量映射。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReleaseBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default);

    /// <summary>
    /// 批量归还已扣减库存（已支付/已发货订单强制取消时调用）。
    /// 逐个 SKU 调用 IInventoryRepository.ReturnDeductedAsync，单个失败记入补偿表。
    /// 与 <see cref="ReleaseBatchAsync"/> 区别：Release 释放的是预占（Reserved），ReturnDeducted 归还的是已扣减（Deducted）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="skuQuantities">SKU 与数量映射。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReturnDeductedBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default);
}
