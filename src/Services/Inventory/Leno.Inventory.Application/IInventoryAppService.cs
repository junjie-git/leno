using Leno.Inventory.Application.DTOs;
using Leno.SharedContracts.Integration.Inventory;

namespace Leno.Inventory.Application;

/// <summary>
/// 库存应用服务接口 —— 预占 / 确认 / 释放 / 归还四个核心用例，库存数量的唯一权威入口。
/// <para>
/// 调用面（双轨下线 DEC-4 后收敛为两种）：
/// ① 同步：<c>Order BC</c> 下单预占经 gRPC <c>InventoryInternalService</c> 调用
///    （<see cref="ReserveAsync"/> 的 bool 语义即下单成败）；
/// ② 异步：MassTransit 命令消费者调用（Confirm / Release，含 ReturnDeducted 操作类型）。
/// </para>
/// <para>
/// 一致性模型：每个用例在**同一数据库事务**内更新台账（订单 × SKU 占用记录）与基线
/// （SKU 计数器），不存在半成功状态；幂等由幂等存储 + 台账唯一约束 + 单向状态机三层保证。
/// </para>
/// </summary>
public interface IInventoryAppService
{
    /// <summary>
    /// 预占库存（下单）：同一事务内写入台账条目并对各 SKU 做原子预占，
    /// 任一 SKU 库存不足则整体回滚（无半成功），返回失败。
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
    /// 确认扣减（支付成功）：订单全部"已预占"条目转为"已确认"，数量移出可用。
    /// 无明细 / 已全部终态时为幂等 no-op。
    /// </summary>
    /// <param name="orderId">关联订单标识（台账按订单解析，命令不携带明细）。</param>
    /// <param name="idempotencyKey">幂等键。</param>
    /// <param name="ct">取消令牌。</param>
    Task ConfirmAsync(Guid orderId, Guid idempotencyKey, CancellationToken ct = default);

    /// <summary>
    /// 释放预占（订单取消/超时）或归还已扣减（已支付订单强制取消/退款），按操作类型区分。
    /// 对应条目不存在或已处于目标终态时为幂等 no-op。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="operationType"><see cref="ReleaseStockOperationType.Release"/> = 释放预占；
    /// <see cref="ReleaseStockOperationType.ReturnDeducted"/> = 归还已扣减。</param>
    /// <param name="idempotencyKey">幂等键。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReleaseAsync(
        Guid orderId,
        ReleaseStockOperationType operationType,
        Guid idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// 查询 SKU 当前可卖量（可用 - 预占）。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    Task<int> GetSellableAsync(Guid skuId, CancellationToken ct = default);
}
