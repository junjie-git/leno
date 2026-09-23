using Leno.Inventory.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Aggregates;

/// <summary>
/// 库存台账条目状态。
/// 预占（Reserved）为初始态；确认（Confirmed）/释放（Released）/归还（Returned）为终态，
/// 状态迁移单向不可逆 —— 终态上重复收到同一命令为幂等 no-op。
/// </summary>
public enum StockReservationStatus
{
    /// <summary>已预占（下单未支付）。</summary>
    Reserved = 0,

    /// <summary>已确认扣减（支付成功，数量移出可用）。</summary>
    Confirmed = 1,

    /// <summary>已释放（订单取消/超时，预占归还可用）。</summary>
    Released = 2,

    /// <summary>已归还（已支付订单强制取消/退款，扣减数量加回可用）。</summary>
    Returned = 3
}

/// <summary>
/// 库存台账条目 —— 订单 × SKU 维度的库存占用记录（幂等与审计的唯一事实来源）。
/// <para>
/// 与基线（<see cref="StockBaseline"/>，SKU 维度计数器）的分工：
/// 台账回答"这笔占用属于哪个订单、处于什么状态"，基线回答"这个 SKU 还剩多少可卖"。
/// 二者在应用服务的同一数据库事务内更新（台账写入 + 基线原子 UPDATE），
/// 漂移在物理上不可能发生，因此不需要对账机器。
/// </para>
/// <para>
/// 幂等模型：唯一约束 (order_id, sku_id) 保证同一订单同一 SKU 只有一行；
/// 状态机单向迁移保证重复命令（确认/释放/归还）在终态上为幂等 no-op。
/// </para>
/// </summary>
public sealed class StockReservation : AggregateRoot
{
    /// <summary>关联订单标识（命令按订单寻址，Inventory 自行解析台账）。</summary>
    public Guid OrderId { get; private set; }

    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>占用数量，须 &gt; 0。</summary>
    public int Quantity { get; private set; }

    /// <summary>台账状态（单向迁移）。</summary>
    public StockReservationStatus Status { get; private set; }

    /// <summary>发起本次占用的幂等键（操作级，确认/释放/归还使用各自独立的键）。</summary>
    public Guid IdempotencyKey { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private StockReservation() { }

    private StockReservation(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建一条"已预占"台账条目。
    /// </summary>
    /// <param name="id">条目标识，由应用层生成。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="quantity">占用数量，须 &gt; 0。</param>
    /// <param name="idempotencyKey">预占操作幂等键。</param>
    public static StockReservation CreateReserved(
        Guid id, Guid orderId, Guid skuId, int quantity, Guid idempotencyKey)
    {
        if (orderId == Guid.Empty)
        {
            throw new InventoryDomainException("OrderId 不可为空", "STOCK_ORDER_EMPTY");
        }

        if (skuId == Guid.Empty)
        {
            throw new InventoryDomainException("SkuId 不可为空", "STOCK_SKU_EMPTY");
        }

        if (quantity <= 0)
        {
            throw new InventoryDomainException("占用数量须大于 0", "STOCK_RESERVE_QTY_INVALID");
        }

        return new StockReservation(id)
        {
            OrderId = orderId,
            SkuId = skuId,
            Quantity = quantity,
            Status = StockReservationStatus.Reserved,
            IdempotencyKey = idempotencyKey
        };
    }

    /// <summary>
    /// 迁移到已确认（支付成功，预占转真实扣减）。
    /// </summary>
    public void MarkConfirmed()
        => Transition(StockReservationStatus.Confirmed);

    /// <summary>
    /// 迁移到已释放（订单取消/超时，预占归还可用）。
    /// </summary>
    public void MarkReleased()
        => Transition(StockReservationStatus.Released);

    /// <summary>
    /// 迁移到已归还（已支付订单强制取消/退款，扣减数量加回可用）。
    /// 仅已确认（Confirmed）条目可归还 —— 归还的是已扣减数量，预占未确认的应走释放。
    /// </summary>
    public void MarkReturned()
    {
        if (Status != StockReservationStatus.Confirmed)
        {
            throw new InventoryDomainException(
                $"仅已确认（扣减）条目可归还，当前状态 {Status}", "STOCK_RETURN_INVALID_STATE");
        }

        Status = StockReservationStatus.Returned;
    }

    /// <summary>
    /// 状态单向迁移：Reserved → Confirmed/Released；Confirmed → Returned。
    /// 终态上的重复迁移为幂等 no-op（返回 false 表示本次调用未产生变更）。
    /// </summary>
    /// <returns>是否实际发生迁移。</returns>
    public bool Transition(StockReservationStatus target)
    {
        // 幂等 no-op：目标状态与当前一致（重复命令）
        if (Status == target)
        {
            return false;
        }

        var allowed = Status switch
        {
            StockReservationStatus.Reserved => target is StockReservationStatus.Confirmed
                or StockReservationStatus.Released,
            StockReservationStatus.Confirmed => target is StockReservationStatus.Returned,
            _ => false
        };

        if (!allowed)
        {
            throw new InventoryDomainException(
                $"非法的台账状态迁移：{Status} → {target}", "STOCK_LEDGER_TRANSITION_INVALID");
        }

        Status = target;
        return true;
    }
}
