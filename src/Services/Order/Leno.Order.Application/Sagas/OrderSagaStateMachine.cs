using System.Text.Json;
using Leno.Order.Application.Sagas.Events;
using Leno.Order.Application.Sagas.States;
using Leno.SharedContracts.Events;
using Leno.SharedContracts.Integration.Inventory;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Application.Sagas;

/// <summary>
/// 订单 Saga 状态机，编排下单全流程：库存预占 → 积分冻结 → 订单创建 → 支付成功 → 完成。
/// 任一阶段失败时进入 Compensating 状态，反向释放已成功资源（库存 / 积分 / 优惠券），最终 Compensated。
/// 状态持久化到 <c>order_saga_states</c> 表（MassTransit EF Core Saga），服务崩溃重启后从持久化状态继续。
/// 双轨期：feature flag <c>Order:UseSagaStateMachine</c> 切流；flag=true 时由 OrderSagaOrchestrator thin wrapper
/// 发布 <see cref="OrderSagaStarted"/> 启动本状态机，flag=false 时走旧进程内编排路径。
/// </summary>
public sealed class OrderSagaStateMachine : MassTransitStateMachine<OrderSagaState>
{
    /// <summary>状态名称常量，与 <see cref="OrderSagaState.CurrentState"/> 持久化值一致。</summary>
    public static class StateNames
    {
        public const string Pending = "Pending";
        public const string StockReserved = "StockReserved";
        public const string PointsFrozen = "PointsFrozen";
        public const string OrderCreated = "OrderCreated";
        public const string Completed = "Completed";
        public const string Compensating = "Compensating";
        public const string Compensated = "Compensated";
    }

    private readonly ILogger<OrderSagaStateMachine> _logger;

    /// <summary>库存已预占状态：等待 <see cref="StockReservedIntegrationEvent"/> 推进。</summary>
    public State StockReserved { get; private set; } = default!;

    /// <summary>积分已冻结状态：等待 <see cref="PointsFrozen"/> 推进。</summary>
    public State PointsFrozen { get; private set; } = default!;

    /// <summary>订单聚合已创建状态：等待 <see cref="PaymentSucceededEvent"/> 推进。</summary>
    public State OrderCreated { get; private set; } = default!;

    /// <summary>订单已完成终态。</summary>
    public State Completed { get; private set; } = default!;

    /// <summary>补偿中状态：反向释放已成功资源。</summary>
    public State Compensating { get; private set; } = default!;

    /// <summary>补偿完成终态。</summary>
    public State Compensated { get; private set; } = default!;

    /// <summary>Saga 启动事件，由 OrderSagaOrchestrator thin wrapper 发布。</summary>
    public Event<OrderSagaStarted> OrderStarted { get; private set; } = default!;

    /// <summary>库存预占成功集成事件（Inventory BC → Order BC）。</summary>
    public Event<Leno.SharedContracts.Integration.Inventory.StockReservedIntegrationEvent> StockReservedEvent { get; private set; } = default!;

    /// <summary>积分冻结成功事件（Saga 内部，由 OrderSagaOrchestrator 调用 FreezeAsync 后发布）。</summary>
    public Event<PointsFrozen> PointsFrozenEvent { get; private set; } = default!;

    /// <summary>订单聚合已创建事件（Saga 内部，由 OrderSagaOrchestrator 持久化订单后发布）。</summary>
    public Event<OrderAggregateCreated> OrderAggregateCreatedEvent { get; private set; } = default!;

    /// <summary>支付成功集成事件（Payment BC → Order BC），推进 OrderCreated → Completed。</summary>
    public Event<PaymentSucceededEvent> PaymentSucceeded { get; private set; } = default!;

    /// <summary>Saga 补偿请求事件（Saga 内部，由 OrderSagaOrchestrator 在子任务失败时发布）。</summary>
    public Event<SagaCompensationRequested> CompensationRequested { get; private set; } = default!;

    /// <summary>
    /// JSON 序列化选项，统一 <see cref="OrderSagaState.ItemsJson"/> 与 <see cref="OrderSagaState.StockReservationIdsJson"/> 序列化格式。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OrderSagaStateMachine(ILogger<OrderSagaStateMachine> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;

        // InstanceState 使用 string 持久化（OrderSagaState.CurrentState: nvarchar(32)）。
        // 不显式列举状态：MassTransit 按声明的 State 属性名自动映射到字符串。
        // OrderSagaState.CurrentState 默认 "Pending" 仅作为持久化占位，首次持久化时由状态机覆写为实际状态名。
        InstanceState(x => x.CurrentState);

        // 事件路由：按 OrderId 关联 Saga 实例（CorrelationId = OrderId）
        Event(() => OrderStarted, e => e.CorrelateById(c => c.Message.OrderId));
        Event(() => StockReservedEvent, e => e.CorrelateById(c => c.Message.OrderId));
        Event(() => PointsFrozenEvent, e => e.CorrelateById(c => c.Message.OrderId));
        Event(() => OrderAggregateCreatedEvent, e => e.CorrelateById(c => c.Message.OrderId));
        Event(() => PaymentSucceeded, e => e.CorrelateById(c => c.Message.OrderId));
        Event(() => CompensationRequested, e => e.CorrelateById(c => c.Message.OrderId));

        // 进入终态时自动 Finalize，触发 Saga 实例从持久化存储删除（SetCompletedWhenFinalized）
        SetCompletedWhenFinalized();

        Initially(
            When(OrderStarted)
                .Then(c =>
                {
                    c.Saga.OrderId = c.Message.OrderId;
                    c.Saga.UserId = c.Message.UserId;
                    c.Saga.TotalAmount = c.Message.TotalAmount;
                    c.Saga.Currency = string.IsNullOrWhiteSpace(c.Message.Currency) ? "CNY" : c.Message.Currency;
                    c.Saga.ItemsJson = JsonSerializer.Serialize(c.Message.Items, JsonOptions);
                    c.Saga.CreatedAt = DateTime.UtcNow;
                    c.Saga.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Saga 启动 OrderId={OrderId} UserId={UserId} TotalAmount={TotalAmount} Items={ItemCount}",
                        c.Message.OrderId, c.Message.UserId, c.Message.TotalAmount, c.Message.Items.Count);
                })
                .TransitionTo(StockReserved));

        During(StockReserved,
            When(StockReservedEvent)
                .Then(c =>
                {
                    // 持久化已预留 SKU 列表，补偿时用于按 ReservationId 释放
                    c.Saga.StockReservationIdsJson = JsonSerializer.Serialize(c.Message.ReservationItems, JsonOptions);
                    c.Saga.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Saga 库存预占成功 OrderId={OrderId} ReservationItems={Count}",
                        c.Message.OrderId, c.Message.ReservationItems.Count);
                })
                .TransitionTo(PointsFrozen),
            When(CompensationRequested)
                .Then(c => _logger.LogWarning(
                    "Saga 在 StockReserved 状态收到补偿请求 OrderId={OrderId} Reason={Reason}",
                    c.Saga.OrderId, c.Message.Reason))
                .PublishAsync(c => c.Init<ReleaseStockCommand>(new
                {
                    OrderId = c.Saga.OrderId,
                    IdempotencyKey = c.Saga.OrderId,
                    OperationType = ReleaseStockOperationType.Release
                }))
                .PublishAsync(c => c.Init<ReleaseCouponsCommand>(new { OrderId = c.Saga.OrderId }))
                .TransitionTo(Compensated)
                .Finalize());

        During(PointsFrozen,
            When(PointsFrozenEvent)
                .Then(c =>
                {
                    c.Saga.PointsFrozenAmount = c.Message.FrozenAmount;
                    c.Saga.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Saga 积分冻结成功 OrderId={OrderId} FrozenAmount={Amount}",
                        c.Message.OrderId, c.Message.FrozenAmount);
                })
                .TransitionTo(OrderCreated),
            When(CompensationRequested)
                .Then(c => _logger.LogWarning(
                    "Saga 在 PointsFrozen 状态收到补偿请求 OrderId={OrderId} Reason={Reason}",
                    c.Saga.OrderId, c.Message.Reason))
                .PublishAsync(c => c.Init<ReleaseStockCommand>(new
                {
                    OrderId = c.Saga.OrderId,
                    IdempotencyKey = c.Saga.OrderId,
                    OperationType = ReleaseStockOperationType.Release
                }))
                .PublishAsync(c => c.Init<UnfreezePointsCommand>(new { OrderId = c.Saga.OrderId }))
                .PublishAsync(c => c.Init<ReleaseCouponsCommand>(new { OrderId = c.Saga.OrderId }))
                .TransitionTo(Compensated)
                .Finalize());

        During(OrderCreated,
            When(OrderAggregateCreatedEvent)
                .Then(c =>
                {
                    c.Saga.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Saga 订单聚合已创建 OrderId={OrderId}，等待支付成功事件",
                        c.Saga.OrderId);
                }),
            When(PaymentSucceeded)
                .Then(c =>
                {
                    c.Saga.PaymentId = c.Message.PaymentId;
                    c.Saga.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Saga 支付成功 OrderId={OrderId} PaymentId={PaymentId}",
                        c.Message.OrderId, c.Message.PaymentId);
                })
                .PublishAsync(c => c.Init<SagaCompleted>(new
                {
                    OrderId = c.Saga.OrderId,
                    FinalState = StateNames.Completed,
                    Succeeded = true
                }))
                .TransitionTo(Completed)
                .Finalize(),
            When(CompensationRequested)
                .Then(c => _logger.LogWarning(
                    "Saga 在 OrderCreated 状态收到补偿请求 OrderId={OrderId} Reason={Reason}",
                    c.Saga.OrderId, c.Message.Reason))
                .PublishAsync(c => c.Init<ReleaseStockCommand>(new
                {
                    OrderId = c.Saga.OrderId,
                    IdempotencyKey = c.Saga.OrderId,
                    OperationType = ReleaseStockOperationType.Release
                }))
                .PublishAsync(c => c.Init<UnfreezePointsCommand>(new { OrderId = c.Saga.OrderId }))
                .PublishAsync(c => c.Init<ReleaseCouponsCommand>(new { OrderId = c.Saga.OrderId }))
                .TransitionTo(Compensated)
                .Finalize());

        // Completed / Compensated 终态：忽略后续事件（幂等）
        During(Completed, Compensated,
            Ignore(OrderStarted),
            Ignore(StockReservedEvent),
            Ignore(PointsFrozenEvent),
            Ignore(OrderAggregateCreatedEvent),
            Ignore(PaymentSucceeded),
            Ignore(CompensationRequested));

        // Completed 进入时发布 SagaCompleted（补偿路径已在各自状态内发布）
        // （上面的 PaymentSucceeded 分支已发布 SagaCompleted，此处无需重复）
    }
}
