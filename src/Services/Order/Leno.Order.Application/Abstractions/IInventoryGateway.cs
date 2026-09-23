namespace Leno.Order.Application.Abstractions;

/// <summary>
/// 库存防腐层网关 —— Order BC 对 Inventory BC 的**唯一**库存出口（双轨下线 DEC-4，2026-09-22）。
/// <para>
/// 交互契约：
/// ① <see cref="ReserveBatchAsync"/>：下单预占，gRPC 同步调用（<c>InventoryInternalService.ReserveStock</c>），
///    返回 bool 即下单成败 —— 库存不足是下单的正常业务失败；
/// ② <see cref="ConfirmBatchAsync"/>：支付成功确认扣减，MassTransit 命令（异步）；
/// ③ <see cref="ReleaseBatchAsync"/>：释放预占（订单取消/超时），MassTransit 命令（异步）；
/// ④ <see cref="ReturnDeductedBatchAsync"/>：归还已扣减（已支付订单强制取消/退款），MassTransit 命令（异步）。
/// </para>
/// <para>
/// Inventory BC 按订单解析台账（命令不携带 SKU 明细），调用方无需持久化预占记录；
/// 全部操作在 Inventory 侧幂等（台账唯一约束 + 单向状态机），重发安全。
/// </para>
/// </summary>
public interface IInventoryGateway
{
    /// <summary>
    /// 批量预占库存（下单），全部 SKU 预占成功返回 true，任一不足返回 false（整体失败，无半成功）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="skuQuantities">SKU 与预占数量映射。</param>
    /// <param name="ct">取消令牌。</param>
    Task<bool> ReserveBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default);

    /// <summary>
    /// 确认扣减库存（支付成功），将订单预占转为真实扣减（异步命令）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ConfirmBatchAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 释放预占库存（订单取消），回退预占数量（异步命令）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReleaseBatchAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 归还已扣减库存（已支付/已发货订单强制取消时调用），将已扣减数量加回可用（异步命令）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReturnDeductedBatchAsync(Guid orderId, CancellationToken ct = default);
}
