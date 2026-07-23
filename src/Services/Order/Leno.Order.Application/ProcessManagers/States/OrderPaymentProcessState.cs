namespace Leno.Order.Application.ProcessManagers.States;

/// <summary>
/// 订单支付流程编排状态（持久化到 order_payment_processes 表）。
/// 由 <see cref="OrderPaymentProcessManager"/> 驱动，跟踪支付成功后三个并行子任务
/// （<c>MarkOrderPaid</c> / <c>ConfirmStock</c> / <c>ConfirmPoints</c>）的完成进度，
/// 三者全部完成后整体进入 <c>Completed</c>；任一失败进入 <c>Compensating</c> 反向补偿。
/// 与 <c>OrderSagaState</c>（下单全流程 Saga）不同：本状态是 Saga 之上的支付后业务编排层，
/// 不继承 <c>SagaStateMachineInstance</c>，是普通持久化实体。
/// 乐观锁通过 <see cref="RowVersion"/>（rowversion）实现，并发子任务回写时由 EF Core 检测冲突并重试。
/// </summary>
public sealed class OrderPaymentProcessState
{
    /// <summary>
    /// 流程实例标识（主键），每次 <see cref="OrderPaymentProcessManager.StartAsync"/> 时新建。
    /// 与 <see cref="OrderId"/> 一对一（同一订单仅一个进行中的支付流程）。
    /// </summary>
    public Guid ProcessId { get; set; }

    /// <summary>业务订单标识，建立 <see cref="IOrderPaymentProcessRepository.GetByOrderIdAsync"/> 索引。</summary>
    public Guid OrderId { get; set; }

    /// <summary>关联支付单标识，由 <c>PaymentSucceededEvent</c> 携带。</summary>
    public Guid PaymentId { get; set; }

    /// <summary>
    /// 当前状态名称（AwaitingStockConfirm / AwaitingPointsConfirm / AwaitingMarkPaid / Completed / Compensating / Compensated）。
    /// 由 <see cref="OrderPaymentProcessManager.TryCompleteAsync"/> 根据三个完成标志推导写入。
    /// 默认值 <c>AwaitingStockConfirm</c> 与 StartAsync 创建时一致。
    /// </summary>
    public string CurrentState { get; set; } = "AwaitingStockConfirm";

    /// <summary>库存确认子任务是否完成（预占 → 真实扣减）。会员订阅订单无实物库存，由消费者跳过时仍标记完成。</summary>
    public bool StockConfirmed { get; set; }

    /// <summary>积分确认子任务是否完成（冻结 → 正式扣减）。会员订阅订单跳过积分确认，仍标记完成。</summary>
    public bool PointsConfirmed { get; set; }

    /// <summary>订单标记已支付子任务是否完成（订单状态 PendingPayment → Paid）。</summary>
    public bool OrderMarkedPaid { get; set; }

    /// <summary>流程创建时间（UTC），StartAsync 时记录。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>流程最近更新时间（UTC），每次子任务回写或状态流转时刷新。</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 乐观锁版本号（SQL Server rowversion），由 EF Core 自动维护。
    /// 三个子任务可能并发回写状态，EF Core 据此检测并发冲突并重试，保证不丢失更新。
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
