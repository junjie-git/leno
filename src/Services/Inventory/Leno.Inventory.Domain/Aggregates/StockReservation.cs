using Leno.Inventory.Domain.Events;
using Leno.Inventory.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Aggregates;

/// <summary>
/// 库存预占聚合根，按 SKU 维护库存基线、预占与已扣减数量。
/// 不变量：<see cref="AvailableQty"/> = <see cref="BaseLineQty"/> - <see cref="ReservedQty"/> - <see cref="DeductedQty"/> ≥ 0。
/// 预占/确认/释放三阶段模型对应下单预占、支付确认、取消回退。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>StockReservationId</c>，与 <see cref="SkuId"/> 一一对应。
/// </summary>
public sealed class StockReservation : AggregateRoot
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>库存基线（商品域可用库存快照），由 <c>StockAdjustedEvent</c> 驱动同步。</summary>
    public int BaseLineQty { get; private set; }

    /// <summary>已预占数量（下单未支付）。</summary>
    public int ReservedQty { get; private set; }

    /// <summary>已扣减数量（支付确认真实扣减）。</summary>
    public int DeductedQty { get; private set; }

    /// <summary>当前可用数量 = 基线 - 预占 - 已扣减。</summary>
    public int AvailableQty => BaseLineQty - ReservedQty - DeductedQty;

    /// <summary>EF Core 无参构造。</summary>
    private StockReservation() { }

    private StockReservation(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验 SKU 非空、基线 ≥ 0，初始化预占与已扣减为 0。
    /// </summary>
    /// <param name="id">聚合标识，由应用层生成。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="baseLineQty">库存基线，须 ≥ 0。</param>
    public static StockReservation Create(Guid id, Guid skuId, int baseLineQty)
    {
        if (skuId == Guid.Empty)
        {
            throw new InventoryDomainException("SkuId 不可为空", "STOCK_SKU_EMPTY");
        }

        if (baseLineQty < 0)
        {
            throw new InventoryDomainException("库存基线不可为负", "STOCK_BASELINE_INVALID");
        }

        return new StockReservation(id == Guid.Empty ? Guid.NewGuid() : id)
        {
            SkuId = skuId,
            BaseLineQty = baseLineQty,
            ReservedQty = 0,
            DeductedQty = 0
        };
    }

    /// <summary>
    /// 预占库存（下单），校验订单非空、数量 &gt; 0、可用充足，累加预占并发布 <see cref="StockReservedEvent"/>。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="quantity">预占数量，须 &gt; 0。</param>
    public void ReserveStock(Guid orderId, int quantity)
    {
        if (orderId == Guid.Empty)
        {
            throw new InventoryDomainException("OrderId 不可为空", "STOCK_ORDER_EMPTY");
        }

        if (quantity <= 0)
        {
            throw new InventoryDomainException("预占数量须大于 0", "STOCK_RESERVE_QTY_INVALID");
        }

        if (AvailableQty < quantity)
        {
            throw new InventoryDomainException(
                $"库存不足：可用 {AvailableQty}，本次预占 {quantity}",
                "STOCK_INSUFFICIENT");
        }

        ReservedQty += quantity;
        AddDomainEvent(new StockReservedEvent(SkuId, orderId, quantity));
    }

    /// <summary>
    /// 确认扣减（支付成功），校验订单非空、数量 &gt; 0、预占充足，
    /// 预占转已扣减并发布 <see cref="StockConfirmedEvent"/>。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="quantity">确认扣减数量，须 &gt; 0。</param>
    public void ConfirmStockDeduction(Guid orderId, int quantity)
    {
        if (orderId == Guid.Empty)
        {
            throw new InventoryDomainException("OrderId 不可为空", "STOCK_ORDER_EMPTY");
        }

        if (quantity <= 0)
        {
            throw new InventoryDomainException("确认扣减数量须大于 0", "STOCK_CONFIRM_QTY_INVALID");
        }

        if (ReservedQty < quantity)
        {
            throw new InventoryDomainException(
                $"预占不足：已预占 {ReservedQty}，本次确认 {quantity}",
                "STOCK_RESERVED_INSUFFICIENT");
        }

        ReservedQty -= quantity;
        DeductedQty += quantity;
        AddDomainEvent(new StockConfirmedEvent(SkuId, orderId, quantity));
    }

    /// <summary>
    /// 释放预占（订单取消），校验订单非空、数量 &gt; 0、预占充足，扣减预占并发布 <see cref="StockReleasedEvent"/>。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="quantity">释放数量，须 &gt; 0。</param>
    public void ReleaseStock(Guid orderId, int quantity)
    {
        if (orderId == Guid.Empty)
        {
            throw new InventoryDomainException("OrderId 不可为空", "STOCK_ORDER_EMPTY");
        }

        if (quantity <= 0)
        {
            throw new InventoryDomainException("释放数量须大于 0", "STOCK_RELEASE_QTY_INVALID");
        }

        if (ReservedQty < quantity)
        {
            throw new InventoryDomainException(
                $"预占不足：已预占 {ReservedQty}，本次释放 {quantity}",
                "STOCK_RESERVED_INSUFFICIENT");
        }

        ReservedQty -= quantity;
        AddDomainEvent(new StockReleasedEvent(SkuId, orderId, quantity));
    }

    /// <summary>
    /// 同步库存基线，由商品域 <c>StockAdjustedEvent</c> 驱动。
    /// 校验 delta ≠ 0，更新基线并校验基线 ≥ 0。不发布事件（基线变更由上游事件驱动）。
    /// </summary>
    /// <param name="delta">基线增量，正数为补货、负数为扣减，不可为 0。</param>
    public void Replenish(int delta)
    {
        if (delta == 0)
        {
            throw new InventoryDomainException("库存基线增量不可为 0", "STOCK_REPLENISH_DELTA_ZERO");
        }

        BaseLineQty += delta;

        if (BaseLineQty < 0)
        {
            throw new InventoryDomainException(
                $"库存基线不可为负：调整后 {BaseLineQty}",
                "STOCK_BASELINE_INVALID");
        }
    }
}
