using Leno.Product.Domain.Exceptions;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Aggregates;

/// <summary>
/// SKU 库存基线聚合根，权威持有 SKU 的可用、预占与扣减库存。
/// 高频预占由订单域在 Redis 完成，本聚合通过消费订单域库存事件同步基线（最终一致）。
/// 卖家补货/盘点修正直接操作本聚合并发布 <see cref="StockAdjustedEvent"/>。
/// </summary>
public sealed class StockBaseline : AggregateRoot
{
    /// <summary>所属 SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>可用库存（物理在库，可被预占）。</summary>
    public int AvailableQty { get; private set; }

    /// <summary>预占库存（已被未支付订单锁定，待支付扣减或取消释放）。</summary>
    public int ReservedQty { get; private set; }

    /// <summary>已扣减库存（已支付发货，永久移出可用）。</summary>
    public int DeductedQty { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private StockBaseline() { }

    private StockBaseline(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建库存基线，初始预占与扣减均为 0。
    /// </summary>
    /// <param name="baselineId">基线标识，由应用层生成。</param>
    /// <param name="skuId">所属 SKU 标识。</param>
    /// <param name="initialQty">初始可用库存，须 ≥ 0。</param>
    public static StockBaseline Create(Guid baselineId, Guid skuId, int initialQty)
    {
        if (baselineId == Guid.Empty)
        {
            throw new ProductDomainException("库存基线标识不可为空", "STOCK_BASELINE_ID_EMPTY");
        }

        if (skuId == Guid.Empty)
        {
            throw new ProductDomainException("SKU 标识不可为空", "STOCK_SKU_EMPTY");
        }

        if (initialQty < 0)
        {
            throw new ProductDomainException("初始库存不可为负", "STOCK_INITIAL_NEGATIVE");
        }

        return new StockBaseline(baselineId)
        {
            SkuId = skuId,
            AvailableQty = initialQty,
            ReservedQty = 0,
            DeductedQty = 0
        };
    }

    /// <summary>
    /// 补货，可用库存上调并发布 <see cref="StockAdjustedEvent"/> 通知订单域同步。
    /// </summary>
    /// <param name="qty">补货数量，须 > 0。</param>
    public void Replenish(int qty)
    {
        if (qty <= 0)
        {
            throw new ProductDomainException("补货数量须大于 0", "STOCK_REPLENISH_INVALID");
        }

        AvailableQty += qty;

        AddDomainEvent(new StockAdjustedEvent(SkuId, AvailableQty, DateTime.UtcNow));
    }

    /// <summary>
    /// 同步预占库存（消费订单域预占事件，将订单域 Redis 权威值镜像到基线）。
    /// </summary>
    /// <param name="reservedQty">订单域当前预占总量，须 ≥ 0 且 ≤ 可用库存。</param>
    public void SyncReserved(int reservedQty)
    {
        if (reservedQty < 0)
        {
            throw new ProductDomainException("预占库存不可为负", "STOCK_RESERVED_NEGATIVE");
        }

        if (reservedQty > AvailableQty)
        {
            throw new ProductDomainException("预占库存不可超过可用库存", "STOCK_RESERVED_EXCEED");
        }

        ReservedQty = reservedQty;
    }

    /// <summary>
    /// 同步扣减库存（消费订单域支付事件，将预占转为扣减并移出可用）。
    /// </summary>
    /// <param name="deductedQty">订单域当前累计扣减总量，须 ≥ 0。</param>
    public void SyncDeducted(int deductedQty)
    {
        if (deductedQty < 0)
        {
            throw new ProductDomainException("扣减库存不可为负", "STOCK_DEDUCTED_NEGATIVE");
        }

        var delta = deductedQty - DeductedQty;
        if (delta > 0)
        {
            AvailableQty -= delta;
            ReservedQty = Math.Max(0, ReservedQty - delta);
        }

        DeductedQty = deductedQty;

        if (AvailableQty < 0)
        {
            throw new ProductDomainException("可用库存不可为负", "STOCK_AVAILABLE_NEGATIVE");
        }
    }

    /// <summary>
    /// 同步释放库存（消费订单域取消事件，释放对应预占）。
    /// </summary>
    /// <param name="releasedQty">本次释放数量，须 ≥ 0 且 ≤ 当前预占。</param>
    public void SyncReleased(int releasedQty)
    {
        if (releasedQty < 0)
        {
            throw new ProductDomainException("释放数量不可为负", "STOCK_RELEASED_NEGATIVE");
        }

        if (releasedQty > ReservedQty)
        {
            throw new ProductDomainException("释放数量不可超过预占库存", "STOCK_RELEASED_EXCEED");
        }

        ReservedQty -= releasedQty;
    }
}
