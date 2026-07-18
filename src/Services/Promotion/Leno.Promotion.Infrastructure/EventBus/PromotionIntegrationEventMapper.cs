using Leno.Infrastructure.EventBus;
using Leno.Promotion.Domain.Events;
using Leno.SharedContracts.Events;

namespace Leno.Promotion.Infrastructure.EventBus;

/// <summary>
/// Promotion BC 领域事件到集成事件的翻译器。
/// 将促销域聚合（SeckillActivity/UserCoupon）收集的领域事件翻译为 SharedContracts 中的集成事件。
/// </summary>
public class PromotionIntegrationEventMapper : IntegrationEventMapperBase
{
    public PromotionIntegrationEventMapper()
    {
        // SeckillOrderCreatedEvent → SeckillOrderCreatedIntegrationEvent（通知域下单成功通知、订单域异步创建秒杀订单）
        RegisterHandler<SeckillOrderCreatedEvent, SeckillOrderCreatedIntegrationEvent>(e =>
            new SeckillOrderCreatedIntegrationEvent(
                e.ActivityId, e.SpuId, e.SkuId, e.UserId, e.OrderId, e.SeckillPrice, e.Quantity));

        // SeckillOrderConfirmedEvent → SeckillOrderConfirmedIntegrationEvent（促销域标记预占履约；本事件实际发布方为订单域，此处为防御性注册）
        RegisterHandler<SeckillOrderConfirmedEvent, SeckillOrderConfirmedIntegrationEvent>(e =>
            new SeckillOrderConfirmedIntegrationEvent(e.ActivityId, e.OrderId));

        // SeckillOrderCreationFailedEvent → SeckillOrderCreationFailedIntegrationEvent（促销域回退库存；本事件实际发布方为订单域，此处为防御性注册）
        RegisterHandler<SeckillOrderCreationFailedEvent, SeckillOrderCreationFailedIntegrationEvent>(e =>
            new SeckillOrderCreationFailedIntegrationEvent(
                e.ActivityId, e.SkuId, e.UserId, e.OrderId, e.Quantity, e.Reason));

        // SeckillStockSoldOutEvent → SeckillStockSoldOutIntegrationEvent（通知域售罄通知、商品域售罄标记）
        RegisterHandler<SeckillStockSoldOutEvent, SeckillStockSoldOutIntegrationEvent>(e =>
            new SeckillStockSoldOutIntegrationEvent(e.ActivityId, e.SkuId, e.SoldOutAt));

        // CouponExchangeSucceededDomainEvent → CouponExchangeSucceededEvent（积分域正式扣减积分）
        RegisterHandler<CouponExchangeSucceededDomainEvent, CouponExchangeSucceededEvent>(e =>
            new CouponExchangeSucceededEvent(e.ExchangeId, e.UserId, e.CouponId));
    }
}
