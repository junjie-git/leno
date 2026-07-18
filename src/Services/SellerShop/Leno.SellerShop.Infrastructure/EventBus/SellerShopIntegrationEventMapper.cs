using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SellerShop.Domain.Events;

namespace Leno.SellerShop.Infrastructure.EventBus;

/// <summary>
/// SellerShop BC 领域事件到集成事件的翻译器。
/// 将 Shop 聚合收集的领域事件翻译为 SharedContracts 中的集成事件对外发布。
/// </summary>
public class SellerShopIntegrationEventMapper : IntegrationEventMapperBase
{
    public SellerShopIntegrationEventMapper()
    {
        // SellerRegisteredDomainEvent → SellerRegisteredEvent（运营审核队列、消息通知域）
        RegisterHandler<SellerRegisteredDomainEvent, SellerRegisteredEvent>(e =>
            new SellerRegisteredEvent(e.ShopId, e.SellerId, e.UserId, e.ShopName));

        // ShopApprovedDomainEvent → ShopApprovedEvent（用户域分配卖家角色、消息通知域）
        RegisterHandler<ShopApprovedDomainEvent, ShopApprovedEvent>(e =>
            new ShopApprovedEvent(e.ShopId, e.SellerId, e.ShopName));

        // ShopSuspendedDomainEvent → ShopSuspendedEvent（商品域置不可售、消息通知域）
        RegisterHandler<ShopSuspendedDomainEvent, ShopSuspendedEvent>(e =>
            new ShopSuspendedEvent(e.ShopId, e.SellerId));

        // ShopResumedDomainEvent → ShopResumedEvent（商品域恢复可售、消息通知域）
        RegisterHandler<ShopResumedDomainEvent, ShopResumedEvent>(e =>
            new ShopResumedEvent(e.ShopId, e.SellerId));

        // ShopClosedDomainEvent → ShopClosedEvent（商品域下架、用户域移除角色、消息通知域）
        RegisterHandler<ShopClosedDomainEvent, ShopClosedEvent>(e =>
            new ShopClosedEvent(e.ShopId, e.SellerId));

        // QualificationExpiringEvent → QualificationExpiringIntegrationEvent（消息通知域通知卖家更新资质）
        // 注意：当前由 QualificationExpiryReminder 后台服务直接发布集成事件，此翻译规则为防御性注册，
        // 供未来聚合根收集该领域事件时复用。
        RegisterHandler<QualificationExpiringEvent, QualificationExpiringIntegrationEvent>(e =>
            new QualificationExpiringIntegrationEvent(
                e.QualificationId,
                e.ShopId,
                e.SellerId,
                e.QualificationType,
                e.Number,
                e.ExpiryDate,
                e.DaysRemaining));
    }
}
