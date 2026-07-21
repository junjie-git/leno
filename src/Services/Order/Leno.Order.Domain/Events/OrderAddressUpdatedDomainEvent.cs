using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Events;

/// <summary>
/// 订单地址更新领域事件（P2-T33），由 <see cref="Aggregates.Order.UpdateAddress"/> 方法收集。
/// 仅秒杀订单在 <c>PendingPayment</c> 状态下允许更新地址（秒杀下单时使用占位地址，用户支付前补充）。
/// 可由 mapper 翻译为集成事件通知下游域（如物流域预热运费计算、搜索索引更新收货区域）。
/// </summary>
public sealed class OrderAddressUpdatedDomainEvent : DomainEventBase
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>操作人标识（买家 UserId）。</summary>
    public Guid OperatorId { get; init; }

    /// <summary>更新时间（UTC）。</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>新收件人姓名。</summary>
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>新收件人手机号。</summary>
    public string RecipientPhone { get; init; } = string.Empty;

    /// <summary>新省份。</summary>
    public string Province { get; init; } = string.Empty;

    /// <summary>新城市。</summary>
    public string City { get; init; } = string.Empty;

    /// <summary>新区/县。</summary>
    public string District { get; init; } = string.Empty;

    /// <summary>新详细地址。</summary>
    public string Detail { get; init; } = string.Empty;

    public OrderAddressUpdatedDomainEvent(
        Guid orderId,
        Guid operatorId,
        DateTime updatedAt,
        string recipientName,
        string recipientPhone,
        string province,
        string city,
        string district,
        string detail)
        : base(orderId)
    {
        OrderId = orderId;
        OperatorId = operatorId;
        UpdatedAt = updatedAt;
        RecipientName = recipientName ?? string.Empty;
        RecipientPhone = recipientPhone ?? string.Empty;
        Province = province ?? string.Empty;
        City = city ?? string.Empty;
        District = district ?? string.Empty;
        Detail = detail ?? string.Empty;
    }
}
