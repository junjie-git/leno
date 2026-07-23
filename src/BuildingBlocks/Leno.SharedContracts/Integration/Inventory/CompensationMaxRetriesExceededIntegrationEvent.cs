using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Integration.Inventory;

/// <summary>
/// 库存预占回滚补偿达到最大重试次数仍失败集成事件（Inventory BC → 告警/运维域）。
/// Inventory BC 的 <c>StockReservationCompensation</c> 聚合在状态流转到 MaxRetriesExceeded 时发布，
/// 通知告警系统人工介入释放库存。
/// </summary>
public sealed class CompensationMaxRetriesExceededIntegrationEvent : IntegrationEventBase
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

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => CompensationId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public CompensationMaxRetriesExceededIntegrationEvent() : base() { }

    public CompensationMaxRetriesExceededIntegrationEvent(
        Guid compensationId,
        Guid orderId,
        Guid skuId,
        int quantity,
        int retryCount,
        int maxRetries,
        string? lastErrorMessage,
        DateTime occurredAtUtc) : base()
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
