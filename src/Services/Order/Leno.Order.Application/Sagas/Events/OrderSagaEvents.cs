using Leno.SharedContracts.Integration.Inventory;
using MassTransit;

namespace Leno.Order.Application.Sagas.Events;

/// <summary>
/// Saga 启动事件，由 <c>OrderSagaOrchestrator</c> thin wrapper 在创建订单时发布。
/// 携带订单核心信息（OrderId / UserId / TotalAmount / Items / IdempotencyKey），
/// 由 <see cref="OrderSagaStateMachine"/> 在 Initially 状态消费，写入 <c>order_saga_states</c> 表并 TransitionTo(StockReserved)。
/// 关联标识 <see cref="CorrelationId"/> = <see cref="OrderId"/>，供 Saga 状态机路由。
/// </summary>
public sealed record OrderSagaStarted(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderSagaItem> Items,
    Guid IdempotencyKey) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// Saga 内部下单明细项，与 <c>OrderSagaState.ItemsJson</c> 反序列化目标对齐。
/// 由 <see cref="OrderSagaStarted"/> 携带，Saga 持久化时序列化为 JSON。
/// </summary>
public sealed record OrderSagaItem(
    Guid SkuId,
    int Quantity,
    Guid SellerId,
    decimal UnitPrice,
    Guid? SourceCartItemId);

/// <summary>
/// 积分冻结成功事件（Saga 内部）。
/// 旧进程内编排通过 <c>IPointsAntiCorruptionService.FreezeAsync</c> 同步调用积分域，
/// Saga 模式下由 OrderSagaOrchestrator thin wrapper 在调用 FreezeAsync 成功后发布本事件，
/// 推进状态机由 PointsFrozen 状态 → OrderCreated。
/// </summary>
public sealed record PointsFrozen(
    Guid OrderId,
    decimal FrozenAmount) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 订单聚合已创建事件（Saga 内部）。
/// 由 OrderSagaOrchestrator thin wrapper 在 Order 聚合持久化后发布，
/// 推进状态机由 OrderCreated 状态 → 等待 PaymentSucceededEvent。
/// </summary>
public sealed record OrderAggregateCreated(
    Guid OrderId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// Saga 补偿请求事件（Saga 内部）。
/// 当 Saga 编排过程中任一子任务失败（库存预占失败 / 积分冻结失败 / 订单创建失败）时由 OrderSagaOrchestrator 发布，
/// 推进状态机进入 Compensating 状态，发布 <see cref="ReleaseStockCommand"/> 与 <see cref="UnfreezePointsCommand"/>
/// 反向释放已成功资源，最终 TransitionTo(Compensated)。
/// </summary>
public sealed record SagaCompensationRequested(
    Guid OrderId,
    string Reason) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// Saga 完成事件（Saga 内部）。
/// 状态机进入 Completed 或 Compensated 终态时发布，
/// 供 OrderSagaOrchestrator thin wrapper 或监控组件订阅以感知 Saga 结束。
/// </summary>
public sealed record SagaCompleted(
    Guid OrderId,
    string FinalState,
    bool Succeeded) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// Saga 内部命令：释放冻结积分。
/// 由 <see cref="OrderSagaStateMachine"/> 在 Compensating 状态发布，
/// 由 Order BC 内的 <c>UnfreezePointsCommandConsumer</c> 消费并调用 <c>IPointsAntiCorruptionService.ReleaseAsync</c>。
/// 命令而非事件，因释放积分是单向操作（无需返回值），由 MassTransit 路由到对应消费者。
/// </summary>
public sealed record UnfreezePointsCommand(
    Guid OrderId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// Saga 内部命令：释放优惠券。
/// 由 <see cref="OrderSagaStateMachine"/> 在 Compensating 状态发布，
/// 由 Order BC 内的 <c>ReleaseCouponsCommandConsumer</c> 消费并调用 <c>IPromotionAntiCorruptionService.ReleaseCouponsAsync</c>。
/// </summary>
public sealed record ReleaseCouponsCommand(
    Guid OrderId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}
