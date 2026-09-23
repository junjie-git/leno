using Leno.Inventory.Domain.Events;
using Leno.Inventory.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Aggregates;

/// <summary>
/// SKU 库存基线聚合根 —— SKU 维度的库存计数器（可用 / 预占 / 已扣减），库存数量的唯一权威。
/// <para>
/// 与台账（<see cref="StockReservation"/>，订单 × SKU 维度）的分工：
/// 基线回答"这个 SKU 还剩多少可卖"，台账回答"这笔占用属于哪个订单、处于什么状态"。
/// 预占/确认/释放/归还四个高频操作由应用服务在**同一数据库事务**内以原子条件 UPDATE
/// 更新基线、写入/迁移台账（计数器语义不适合走聚合加载-修改-保存，见基线仓储实现）；
/// 本聚合保留创建（首次基线同步）与卖家调整（补货）两个非争用路径。
/// </para>
/// <para>
/// 高频预占由 Order BC 直写 Redis 的旧模式已废除（双轨下线 DEC-4，2026-09-22）：
/// 库存以 Inventory BC 为唯一权威，不再存在"订单域 Redis 权威值镜像"。
/// </para>
/// </summary>
/// <remarks>
/// 本聚合系由 Product BC 迁入 Inventory BC 的统一真源；Product BC 中
/// 旧的 <c>Leno.Product.Domain.Aggregates.StockBaseline</c> 保留只读投影，
/// 待后续任务完成后单独下线（遗留项）。
/// </remarks>
public sealed class StockBaseline : AggregateRoot
{
    /// <summary>所属 SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>所属商品（SPU）标识，用于发布库存调整事件时填充 ProductId。</summary>
    public Guid ProductId { get; private set; }

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
    /// <param name="productId">所属商品（SPU）标识，须非空。</param>
    public static StockBaseline Create(Guid baselineId, Guid skuId, int initialQty, Guid productId)
    {
        if (baselineId == Guid.Empty)
        {
            throw new InventoryDomainException("库存基线标识不可为空", "STOCK_BASELINE_ID_EMPTY");
        }

        if (skuId == Guid.Empty)
        {
            throw new InventoryDomainException("SKU 标识不可为空", "STOCK_SKU_EMPTY");
        }

        if (productId == Guid.Empty)
        {
            throw new InventoryDomainException("商品标识不可为空", "STOCK_PRODUCT_EMPTY");
        }

        if (initialQty < 0)
        {
            throw new InventoryDomainException("初始库存不可为负", "STOCK_INITIAL_NEGATIVE");
        }

        return new StockBaseline(baselineId)
        {
            SkuId = skuId,
            ProductId = productId,
            AvailableQty = initialQty,
            ReservedQty = 0,
            DeductedQty = 0
        };
    }

    /// <summary>
    /// 卖家补货/盘点修正：可用库存按增量上调或下调，并发布 <see cref="StockAdjustedDomainEvent"/>
    /// 通知下游（基线由 Product 侧同步的场景经事件回流）。
    /// </summary>
    /// <param name="delta">库存增量，正数为补货、负数为盘点下调，不可为 0；调整后可用不可为负。</param>
    public void Adjust(int delta)
    {
        if (delta == 0)
        {
            throw new InventoryDomainException("库存调整增量不可为 0", "STOCK_ADJUST_DELTA_ZERO");
        }

        var newAvailable = AvailableQty + delta;
        if (newAvailable < 0)
        {
            throw new InventoryDomainException(
                $"调整后可用库存不可为负：{newAvailable}", "STOCK_AVAILABLE_NEGATIVE");
        }

        AvailableQty = newAvailable;

        AddDomainEvent(new StockAdjustedDomainEvent(Id, SkuId, ProductId, AvailableQty, delta, DateTime.UtcNow));
    }

    /// <summary>
    /// 应用商品域基线同步（消费 <c>StockAdjustedEvent</c>）：将可用库存设置为商品域权威值。
    /// <para>
    /// 语义说明：商品域发布的 <c>StockAdjustedEvent</c> 携带调整后的**可用库存绝对值**，
    /// 本方法直接覆盖 <see cref="AvailableQty"/>；已预占数量（Reserved）不受影响 ——
    /// 预占对应未支付订单，商品侧调价/补货不解除既有占用。
    /// </para>
    /// </summary>
    /// <param name="availableQty">商品域权威可用库存，须 ≥ 0。</param>
    public void ApplyBaselineSync(int availableQty)
    {
        if (availableQty < 0)
        {
            throw new InventoryDomainException("可用库存不可为负", "STOCK_AVAILABLE_NEGATIVE");
        }

        AvailableQty = availableQty;
    }
}
