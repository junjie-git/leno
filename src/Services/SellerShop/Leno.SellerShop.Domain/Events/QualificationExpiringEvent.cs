using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Events;

/// <summary>
/// 资质即将到期领域事件，由 QualificationExpiryReminder 后台服务扫描触发。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.QualificationExpiringIntegrationEvent"/> 集成事件对外发布，
/// 消费方：消息通知域（通知卖家更新资质）。
/// </summary>
public sealed class QualificationExpiringEvent : DomainEventBase
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

    public QualificationExpiringEvent(
        Guid qualificationId,
        Guid shopId,
        Guid sellerId,
        string qualificationType,
        string number,
        DateTime expiryDate,
        int daysRemaining) : base(shopId)
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
