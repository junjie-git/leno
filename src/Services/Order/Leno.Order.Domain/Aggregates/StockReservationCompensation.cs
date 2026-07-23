using Leno.Order.Domain.Events;
using Leno.Order.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Aggregates;

/// <summary>
/// 库存预占回滚补偿记录聚合根（T18）。
/// 当 <c>StockReservationDomainService.ReserveBatchAsync</c> 内部回滚或
/// <c>ReleaseBatchAsync</c>（Saga 补偿/单组回滚）调用 <c>IInventoryRepository.ReleaseAsync</c> 失败时，
/// 将待释放的 SKU 数量写入此补偿表，由后台任务 <c>StockReservationCompensationBackgroundService</c> 定期重试，
/// 保证库存最终被释放（避免库存被无效占用）。
/// 不变量：<see cref="Status"/> 仅可由 Pending → Succeeded / MaxRetriesExceeded 单向流转；
/// <see cref="RetryCount"/> ≤ <see cref="MaxRetries"/>。
/// </summary>
public sealed class StockReservationCompensation : AggregateRoot
{
    /// <summary>默认最大重试次数。</summary>
    public const int DefaultMaxRetries = 5;

    /// <summary>关联订单标识（回滚失败时的目标订单）。</summary>
    public Guid OrderId { get; private set; }

    /// <summary>待释放库存的 SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>待释放数量，须 &gt; 0。</summary>
    public int Quantity { get; private set; }

    /// <summary>补偿状态。</summary>
    public CompensationStatus Status { get; private set; }

    /// <summary>
    /// 已重试次数。底层为 <c>_retryCount</c> 字段，<see cref="MarkFailed"/> 通过
    /// <see cref="Interlocked.Increment(ref int)"/> 原子自增，避免并发场景下非原子 ++ 导致的计数错乱。
    /// </summary>
    public int RetryCount => _retryCount;

    private int _retryCount;

    /// <summary>最大重试次数，超过即标记 MaxRetriesExceeded 等待人工介入。</summary>
    public int MaxRetries { get; private set; }

    /// <summary>
    /// 补偿操作类型，决定后台任务重试时调用哪个库存仓储方法（NEW-P0-3）。
    /// - <see cref="CompensationOperationType.Release"/> → <c>IInventoryRepository.ReleaseAsync</c>（释放预占）
    /// - <see cref="CompensationOperationType.ReturnDeducted"/> → <c>IInventoryRepository.ReturnDeductedAsync</c>（归还已扣减）
    /// </summary>
    public CompensationOperationType OperationType { get; private set; }

    /// <summary>最近一次尝试时间（UTC）。</summary>
    public DateTime? LastAttemptedAt { get; private set; }

    /// <summary>最近一次失败原因（截断存储）。</summary>
    public string? LastErrorMessage { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private StockReservationCompensation() { }

    private StockReservationCompensation(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建一条 Pending 补偿记录。
    /// </summary>
    /// <param name="id">聚合标识，由调用方生成。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="skuId">待释放 SKU 标识。</param>
    /// <param name="quantity">待释放数量，须 &gt; 0。</param>
    /// <param name="maxRetries">最大重试次数，默认 <see cref="DefaultMaxRetries"/>。</param>
    /// <param name="operationType">补偿操作类型，默认 <see cref="CompensationOperationType.Release"/>（NEW-P0-3）。</param>
    public static StockReservationCompensation Create(
        Guid id,
        Guid orderId,
        Guid skuId,
        int quantity,
        int maxRetries = DefaultMaxRetries,
        CompensationOperationType operationType = CompensationOperationType.Release)
    {
        if (orderId == Guid.Empty)
        {
            throw new OrderDomainException("OrderId 不可为空", "STOCK_COMPENSATION_ORDER_EMPTY");
        }

        if (skuId == Guid.Empty)
        {
            throw new OrderDomainException("SkuId 不可为空", "STOCK_COMPENSATION_SKU_EMPTY");
        }

        if (quantity <= 0)
        {
            throw new OrderDomainException("补偿数量须大于 0", "STOCK_COMPENSATION_QTY_INVALID");
        }

        if (maxRetries <= 0)
        {
            maxRetries = DefaultMaxRetries;
        }

        if (!Enum.IsDefined(typeof(CompensationOperationType), operationType))
        {
            throw new OrderDomainException("补偿操作类型无效", "STOCK_COMPENSATION_OP_TYPE_INVALID");
        }

        return new StockReservationCompensation(id == Guid.Empty ? Guid.NewGuid() : id)
        {
            OrderId = orderId,
            SkuId = skuId,
            Quantity = quantity,
            Status = CompensationStatus.Pending,
            _retryCount = 0,
            MaxRetries = maxRetries,
            OperationType = operationType,
            LastAttemptedAt = null,
            LastErrorMessage = null
        };
    }

    /// <summary>
    /// 记录一次重试失败：通过 <see cref="Interlocked.Increment(ref int)"/> 原子递增 <see cref="RetryCount"/>、
    /// 更新 <see cref="LastAttemptedAt"/> 与 <see cref="LastErrorMessage"/>。
    /// 达到 <see cref="MaxRetries"/> 时自动流转到 <see cref="CompensationStatus.MaxRetriesExceeded"/>，
    /// 并发布 <see cref="CompensationMaxRetriesExceededDomainEvent"/> 上报告警供运维人工介入。
    /// 并发更新覆盖由 BaseDbContext 的 Version shadow property（IsRowVersion）保证：并发写入抛
    /// <c>DbUpdateConcurrencyException</c>，由后台任务捕获后下一轮重试。
    /// </summary>
    /// <param name="errorMessage">本次失败原因。</param>
    public void MarkFailed(string? errorMessage)
    {
        if (Status == CompensationStatus.Succeeded)
        {
            return;
        }

        // 原子自增避免并发 MarkFailed 调用导致的计数丢失（P1-T20）
        var currentRetry = Interlocked.Increment(ref _retryCount);
        LastAttemptedAt = DateTime.UtcNow;
        LastErrorMessage = string.IsNullOrEmpty(errorMessage)
            ? null
            : (errorMessage.Length > 500 ? errorMessage[..500] : errorMessage);

        if (currentRetry >= MaxRetries)
        {
            Status = CompensationStatus.MaxRetriesExceeded;
            // 流转到终态时上报告警领域事件，供 Outbox 同事务发布至告警通道
            AddDomainEvent(new CompensationMaxRetriesExceededDomainEvent(
                compensationId: Id,
                orderId: OrderId,
                skuId: SkuId,
                quantity: Quantity,
                retryCount: currentRetry,
                maxRetries: MaxRetries,
                lastErrorMessage: LastErrorMessage,
                occurredAtUtc: LastAttemptedAt.Value));
        }
        else
        {
            Status = CompensationStatus.Pending;
        }
    }

    /// <summary>
    /// 标记补偿成功，状态流转到 <see cref="CompensationStatus.Succeeded"/>（终态）。
    /// </summary>
    public void MarkSucceeded()
    {
        if (Status == CompensationStatus.Succeeded)
        {
            return;
        }

        LastAttemptedAt = DateTime.UtcNow;
        Status = CompensationStatus.Succeeded;
    }
}

/// <summary>
/// 库存预占回滚补偿状态枚举。
/// </summary>
public enum CompensationStatus
{
    /// <summary>待重试（初始状态或上次失败但未达最大重试次数）。</summary>
    Pending = 0,

    /// <summary>已成功释放（终态）。</summary>
    Succeeded = 1,

    /// <summary>达到最大重试次数仍失败，等待人工介入（终态）。</summary>
    MaxRetriesExceeded = 2
}

/// <summary>
/// 库存预占回滚补偿操作类型枚举（NEW-P0-3）。
/// 决定后台补偿任务重试时调用哪个库存仓储方法：
/// <see cref="Release"/> 对应 <c>IInventoryRepository.ReleaseAsync</c>（释放预占），
/// <see cref="ReturnDeducted"/> 对应 <c>IInventoryRepository.ReturnDeductedAsync</c>（归还已扣减）。
/// </summary>
public enum CompensationOperationType
{
    /// <summary>
    /// 释放预占库存，对应 <c>IInventoryRepository.ReleaseAsync</c>。
    /// 适用于 <c>ReserveBatchAsync</c> 内部回滚失败或 <c>ReleaseBatchAsync</c> 失败的场景。
    /// </summary>
    Release = 0,

    /// <summary>
    /// 归还已扣减库存，对应 <c>IInventoryRepository.ReturnDeductedAsync</c>。
    /// 适用于 <c>ReturnDeductedBatchAsync</c>（ForceCancel 已支付订单）失败的场景。
    /// </summary>
    ReturnDeducted = 1
}
