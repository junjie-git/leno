using Leno.Infrastructure.EventBus;
using Leno.Order.Domain.Events;
using Leno.SharedContracts.Events;

namespace Leno.Order.Infrastructure.EventBus;

/// <summary>
/// Order BC 领域事件到集成事件的翻译器。
/// 将 Order 聚合与 StockReservation 聚合收集的领域事件翻译为 SharedContracts 中的集成事件。
/// StockReservedEvent/StockReleasedEvent/StockConfirmedEvent 经 Outbox 持久化，供对账/审计域与未来跨上下文消费方订阅。
/// </summary>
public class OrderIntegrationEventMapper : IntegrationEventMapperBase
{
    public OrderIntegrationEventMapper()
    {
        // OrderCreatedDomainEvent → OrderCreatedEvent（购物车域清空已结算项、促销域锁定优惠券、积分与会员域冻结积分、消息通知域下单成功通知、MQ 30 分钟超时取消）
        RegisterHandler<OrderCreatedDomainEvent, OrderCreatedEvent>(e =>
            new OrderCreatedEvent(
                e.OrderId, e.BuyerId, e.SellerId, e.TotalAmount,
                e.Currency, e.CreatedAt, e.SourceCartItemIds));

        // OrderPaidDomainEvent → OrderPaidEvent（促销域核销优惠券、积分与会员域正式扣减冻结抵现积分/开通会员、卖家域通知发货、消息通知域支付成功通知、库存确认真实扣减）
        RegisterHandler<OrderPaidDomainEvent, OrderPaidEvent>(e =>
            new OrderPaidEvent(
                e.OrderId, e.UserId, e.SellerId, e.PaymentId,
                e.Channel, e.PaidAt, e.TradeNo, e.Amount, e.Currency));

        // OrderShippedDomainEvent → OrderShippedEvent（消息通知域发货通知、卖家域待发货数 -1）
        RegisterHandler<OrderShippedDomainEvent, OrderShippedEvent>(e =>
            new OrderShippedEvent(e.OrderId, e.UserId, e.SellerId, e.LogisticsNo, e.ShippedAt));

        // OrderCompletedDomainEvent → OrderCompletedEvent（卖家域维护店铺销量与销售额）
        RegisterHandler<OrderCompletedDomainEvent, OrderCompletedEvent>(e =>
            new OrderCompletedEvent(
                e.OrderId, e.UserId, e.SellerId, e.TotalAmount, e.Currency, e.CompletedAt));

        // OrderCancelledDomainEvent → OrderCancelledEvent（促销域退还锁定优惠券、积分与会员域释放冻结抵现积分、库存释放预占、消息通知域取消通知）
        RegisterHandler<OrderCancelledDomainEvent, OrderCancelledEvent>(e =>
            new OrderCancelledEvent(
                e.OrderId, e.SellerId, e.CancelReason, e.CancelledAt,
                e.CancelledBy, e.PointsToRelease));

        // OrderAfterSalesWindowClosedDomainEvent → OrderAfterSalesWindowClosedEvent（积分与会员域确认发放购物积分、卖家域结算货款）
        RegisterHandler<OrderAfterSalesWindowClosedDomainEvent, OrderAfterSalesWindowClosedEvent>(e =>
            new OrderAfterSalesWindowClosedEvent(e.OrderId, e.UserId, e.PaidAmount, e.ClosedAt));

        // SeckillOrderConfirmedDomainEvent → SeckillOrderConfirmedIntegrationEvent（促销域标记预占记录为已履约，补偿任务跳过）
        RegisterHandler<SeckillOrderConfirmedDomainEvent, SeckillOrderConfirmedIntegrationEvent>(e =>
            new SeckillOrderConfirmedIntegrationEvent(e.ActivityId, e.OrderId));

        // PaymentRequestedDomainEvent → PaymentRequestedIntegrationEvent（支付域创建支付单并拉起第三方支付）
        RegisterHandler<PaymentRequestedDomainEvent, PaymentRequestedIntegrationEvent>(e =>
            new PaymentRequestedIntegrationEvent(
                e.OrderId, e.UserId, e.Amount, e.Currency, e.Channel, e.RequestedAt));

        // RefundRequestedDomainEvent → RefundRequestedIntegrationEvent（支付域创建退款单）
        RegisterHandler<RefundRequestedDomainEvent, RefundRequestedIntegrationEvent>(e =>
            new RefundRequestedIntegrationEvent(
                e.RefundId, e.OrderId, e.UserId, e.AfterSalesId,
                e.PaymentId, e.RefundAmount, e.Currency, e.Channel, e.RefundReason));

        // StockReservedEvent → StockReservedIntegrationEvent（对账/审计域库存对账、未来跨上下文消费方）
        RegisterHandler<StockReservedEvent, StockReservedIntegrationEvent>(e =>
            new StockReservedIntegrationEvent(e.SkuId, e.OrderId, e.Quantity));

        // StockConfirmedEvent → StockConfirmedIntegrationEvent（对账/审计域库存对账、未来跨上下文消费方）
        RegisterHandler<StockConfirmedEvent, StockConfirmedIntegrationEvent>(e =>
            new StockConfirmedIntegrationEvent(e.SkuId, e.OrderId, e.Quantity));

        // StockReleasedEvent → StockReleasedIntegrationEvent（对账/审计域库存对账、未来跨上下文消费方）
        RegisterHandler<StockReleasedEvent, StockReleasedIntegrationEvent>(e =>
            new StockReleasedIntegrationEvent(e.SkuId, e.OrderId, e.Quantity));
    }
}
