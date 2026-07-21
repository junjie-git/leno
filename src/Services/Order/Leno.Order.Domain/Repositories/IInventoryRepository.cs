namespace Leno.Order.Domain.Repositories;

/// <summary>
/// 库存仓储接口，封装 <see cref="Aggregates.StockReservation"/> 聚合的预占/确认/释放/基线同步操作。
/// 不继承 <see cref="Leno.SharedKernel.Abstractions.IRepository{T}"/>，因其操作以 SKU 维度而非聚合标识维度。
/// </summary>
public interface IInventoryRepository
{
    /// <summary>
    /// 预占库存（下单），原子校验可用充足并预占，成功返回 true。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="quantity">预占数量，须 &gt; 0。</param>
    Task<bool> ReserveAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// 确认扣减库存（支付成功），预占转真实扣减。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="quantity">确认数量，须 &gt; 0。</param>
    Task ConfirmAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// 释放预占库存（订单取消）。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="quantity">释放数量，须 &gt; 0。</param>
    Task ReleaseAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// 归还已扣减库存（已支付/已发货订单强制取消时调用），将已扣减数量加回可用库存。
    /// 与 <see cref="ReleaseAsync"/> 区别：ReleaseAsync 释放的是预占（Reserved），ReturnDeductedAsync 归还的是已扣减（Deducted）。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="quantity">归还数量，须 &gt; 0。</param>
    Task ReturnDeductedAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// 查询 SKU 当前可用库存。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    Task<int> GetAvailableAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 同步库存基线（由商品域 <c>StockAdjustedEvent</c> 驱动）。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="availableQty">商品域可用库存量。</param>
    Task SetBaseLineAsync(Guid skuId, int availableQty, CancellationToken ct = default);
}
