using MassTransit;

namespace Leno.Order.Application.Sagas.States;

/// <summary>
/// 订单 Saga 状态机实例（持久化到 order_saga_states 表）。
/// 关联标识 <see cref="CorrelationId"/> 与业务 <see cref="OrderId"/> 一致，
/// 由 <see cref="OrderSagaStateMachine"/> 驱动状态流转：
/// Pending → StockReserved → PointsFrozen → OrderCreated → Completed
/// 任何阶段失败：当前状态 → Compensating → Compensated。
/// 崩溃恢复：MassTransit EF Core Saga 持久化保证服务重启后从 <see cref="CurrentState"/> 继续。
/// 乐观锁通过 <see cref="Version"/>（rowversion）实现，MassTransit EF Core Saga Repository 在 SaveChanges 时检测并发冲突并重试。
/// </summary>
public sealed class OrderSagaState : SagaStateMachineInstance
{
    /// <summary>
    /// Saga 关联标识，与 <see cref="OrderId"/> 一致，由 <see cref="OrderSagaStarted"/> 携带。
    /// 作为 order_saga_states 表主键（correlation_id 列）。
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// 当前状态名称（Pending / StockReserved / PointsFrozen / OrderCreated / Completed / Compensating / Compensated）。
    /// 由 <see cref="MassTransitStateMachine{TInstance}.InstanceState"/> 写入，持久化到 current_state 列。
    /// 默认值 "Pending" 与 <see cref="OrderSagaStateMachine"/> Initially 状态一致。
    /// </summary>
    public string CurrentState { get; set; } = "Pending";

    /// <summary>业务订单标识，与 <see cref="CorrelationId"/> 一致，单独保留便于查询与对账。</summary>
    public Guid OrderId { get; set; }

    /// <summary>买家账号标识（Order BC 内 Guid 表示）。</summary>
    public Guid UserId { get; set; }

    /// <summary>订单总金额（实付），用于积分冻结与对账。</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 序列化的下单明细列表（JSON），用于崩溃恢复后重建上下文。
    /// 序列化格式：<see cref="System.Text.Json.JsonSerializer"/> 序列化 <c>IReadOnlyList{OrderSagaItem}</c>。
    /// </summary>
    public string ItemsJson { get; set; } = "[]";

    /// <summary>
    /// 已预留的库存 ReservationId 列表（JSON）。
    /// 由 <see cref="StockReservedIntegrationEvent"/> 携带的 ReservationItems 序列化得到，
    /// 补偿时用于按 ReservationId 释放库存。
    /// </summary>
    public string? StockReservationIdsJson { get; set; }

    /// <summary>已冻结的积分金额（折现金额），由 PointsFrozen 事件携带。</summary>
    public decimal PointsFrozenAmount { get; set; }

    /// <summary>关联支付单标识，支付成功前为 null。</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>Saga 创建时间（UTC），首次进入 Pending 状态时记录。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Saga 最近更新时间（UTC），每次状态流转时刷新。</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 乐观锁版本号（SQL Server rowversion），由 EF Core 自动维护。
    /// MassTransit EF Core Saga Repository 据此检测并发冲突并重试，保证并发状态流转不会丢失更新。
    /// </summary>
    public byte[] Version { get; set; } = Array.Empty<byte>();
}
