using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Events;

/// <summary>
/// 库存预占回滚补偿记录达到最大重试次数仍失败领域事件。
/// 由 <see cref="Aggregates.StockReservationCompensation.MarkFailed"/> 在状态流转到
/// <see cref="Aggregates.CompensationStatus.MaxRetriesExceeded"/> 时收集，上报告警供运维人工介入。
/// </summary>
public sealed class CompensationMaxRetriesExceededDomainEvent : DomainEventBase
{
    /// <summary>补偿记录聚合标识。</summary>
    public Guid CompensationId { get; init; }

    /// <summary>关联订单标识（回滚失败的目标订单）。</summary>
    public Guid OrderId { get; init; }

    /// <summary>待释放库存的 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>待释放数量。</summary>
    public int Quantity { get; init; }

    /// <summary>已达的最大重试次数。</summary>
    public int RetryCount { get; init; }

    /// <summary>配置的最大重试次数上限。</summary>
    public int MaxRetries { get; init; }

    /// <summary>最近一次失败原因（截断后）。</summary>
    public string? LastErrorMessage { get; init; }

    /// <summary>事件发生时间（UTC）。</summary>
    public DateTime OccurredAtUtc { get; init; }

    public CompensationMaxRetriesExceededDomainEvent(
        Guid compensationId,
        Guid orderId,
        Guid skuId,
        int quantity,
        int retryCount,
        int maxRetries,
        string? lastErrorMessage,
        DateTime occurredAtUtc)
        : base(compensationId)
    {
        CompensationId = compensationId;
        OrderId = orderId;
        SkuId = skuId;
        Quantity = quantity;
        RetryCount = retryCount;
        MaxRetries = maxRetries;
        LastErrorMessage = lastErrorMessage;
        OccurredAtUtc = occurredAtUtc;
    }
}
