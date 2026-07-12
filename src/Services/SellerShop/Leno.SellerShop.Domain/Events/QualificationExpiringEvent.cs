using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Events;

/// <summary>
/// 资质即将到期集成事件，由 QualificationExpiryReminder 后台服务扫描触发。
/// 消费方：消息通知域（通知卖家更新资质）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class QualificationExpiringEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>资质标识。</summary>
    public Guid QualificationId { get; init; }

    /// <summary>所属店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识。</summary>
    public Guid SellerId { get; init; }

    /// <summary>资质类型。</summary>
    public string QualificationType { get; init; } = string.Empty;

    /// <summary>资质编号。</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>到期日期（UTC）。</summary>
    public DateTime ExpiryDate { get; init; }

    /// <summary>距离到期剩余天数。</summary>
    public int DaysRemaining { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ShopId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public QualificationExpiringEvent() : base()
    {
    }

    public QualificationExpiringEvent(
        Guid qualificationId,
        Guid shopId,
        Guid sellerId,
        string qualificationType,
        string number,
        DateTime expiryDate,
        int daysRemaining) : base()
    {
        QualificationId = qualificationId;
        ShopId = shopId;
        SellerId = sellerId;
        QualificationType = qualificationType;
        Number = number;
        ExpiryDate = expiryDate;
        DaysRemaining = daysRemaining;
    }
}