using Leno.Infrastructure.EventBus;
using Leno.ReviewAfterSales.Domain.Events;
using Leno.SharedContracts.Events;

namespace Leno.ReviewAfterSales.Infrastructure.EventBus;

/// <summary>
/// ReviewAfterSales BC 领域事件到集成事件的翻译器。
/// 将 AfterSales/Review 聚合收集的领域事件翻译为 SharedContracts 中的集成事件对外发布。
/// </summary>
public class ReviewAfterSalesIntegrationEventMapper : IntegrationEventMapperBase
{
    public ReviewAfterSalesIntegrationEventMapper()
    {
        // === AfterSales 聚合 ===

        // AfterSalesSubmittedDomainEvent → AfterSalesSubmittedEvent（卖家/运营处理队列、消息通知域）
        RegisterHandler<AfterSalesSubmittedDomainEvent, AfterSalesSubmittedEvent>(e =>
            new AfterSalesSubmittedEvent(
                e.AfterSalesId, e.OrderId, e.OrderLineId, e.UserId,
                e.SellerId, e.Type, e.RequestedAmount, e.Currency));

        // AfterSalesApprovedDomainEvent → AfterSalesApprovedEvent（消息通知域通知买家退货/退款）
        RegisterHandler<AfterSalesApprovedDomainEvent, AfterSalesApprovedEvent>(e =>
            new AfterSalesApprovedEvent(
                e.AfterSalesId, e.OrderId, e.UserId, e.SellerId,
                e.ApprovedAmount, e.Currency, e.Type));

        // AfterSalesRejectedDomainEvent → AfterSalesRejectedEvent（消息通知域通知买家驳回原因）
        RegisterHandler<AfterSalesRejectedDomainEvent, AfterSalesRejectedEvent>(e =>
            new AfterSalesRejectedEvent(e.AfterSalesId, e.OrderId, e.UserId, e.RejectReason));

        // AfterSalesReturnedDomainEvent → AfterSalesReturnedEvent（消息通知域通知卖家确认收货）
        RegisterHandler<AfterSalesReturnedDomainEvent, AfterSalesReturnedEvent>(e =>
            new AfterSalesReturnedEvent(e.AfterSalesId, e.OrderId, e.SellerId, e.TrackingNo));

        // AfterSalesReturnConfirmedDomainEvent → AfterSalesReturnConfirmedEvent（消息通知域通知买家、支付域准备退款）
        RegisterHandler<AfterSalesReturnConfirmedDomainEvent, AfterSalesReturnConfirmedEvent>(e =>
            new AfterSalesReturnConfirmedEvent(e.AfterSalesId, e.OrderId, e.UserId, e.RefundAmount));

        // AfterSalesRefundFailedDomainEvent → AfterSalesRefundFailedEvent（消息通知域通知买家退款失败、订单域更新售后状态视图）
        RegisterHandler<AfterSalesRefundFailedDomainEvent, AfterSalesRefundFailedEvent>(e =>
            new AfterSalesRefundFailedEvent(e.AfterSalesId, e.OrderId, e.UserId, e.Reason));

        // AfterSalesCancelledDomainEvent → AfterSalesCancelledEvent（消息通知域通知卖家、促销域释放优惠券锁定）
        RegisterHandler<AfterSalesCancelledDomainEvent, AfterSalesCancelledEvent>(e =>
            new AfterSalesCancelledEvent(e.AfterSalesId, e.OrderId, e.UserId, e.SellerId, e.Reason));

        // AfterSalesRefundCompletedDomainEvent → RefundCompletedEvent（订单域回滚销量、促销域退还优惠券、消息通知域退款到账通知）
        // 注意：RefundCompletedEvent 同时由支付域 RefundOrder 聚合发布，本规则表达售后域视角的退款完成事实。
        RegisterHandler<AfterSalesRefundCompletedDomainEvent, RefundCompletedEvent>(e =>
            new RefundCompletedEvent(
                e.OrderId, e.UserId, e.RefundId, e.AfterSalesId,
                e.RefundAmount, e.Currency, e.CompletedAt));

        // AfterSalesRefundRequestedDomainEvent → RefundRequestedIntegrationEvent（支付域创建退款单执行退款）
        // 注意：RefundRequestedIntegrationEvent 同时由订单域发布，本规则表达售后域视角的退款请求事实。
        RegisterHandler<AfterSalesRefundRequestedDomainEvent, RefundRequestedIntegrationEvent>(e =>
            new RefundRequestedIntegrationEvent(
                e.RefundId, e.OrderId, e.UserId, e.AfterSalesId,
                e.PaymentId, e.RefundAmount, e.Currency, e.Channel, e.RefundReason));

        // === Review 聚合 ===

        // ReviewSubmittedDomainEvent → ReviewSubmittedEvent（商品域回写商品评分摘要 score、reviewCount、好评率）
        RegisterHandler<ReviewSubmittedDomainEvent, ReviewSubmittedEvent>(e =>
            new ReviewSubmittedEvent(
                e.ReviewId, e.UserId, e.SpuId, e.Rating, e.NewScore, e.ReviewCount));

        // ReviewApprovedDomainEvent → ReviewApprovedEvent（积分域发放评价积分、商品域重算评分摘要、消息通知域）
        RegisterHandler<ReviewApprovedDomainEvent, ReviewApprovedEvent>(e =>
            new ReviewApprovedEvent(e.ReviewId, e.UserId, e.SpuId, e.Rating));

        // ReviewHiddenDomainEvent → ReviewHiddenEvent（商品域从评分统计中移除该评价）
        RegisterHandler<ReviewHiddenDomainEvent, ReviewHiddenEvent>(e =>
            new ReviewHiddenEvent(e.ReviewId, e.SpuId, e.Rating));

        // ReviewModeratedDomainEvent → ReviewModeratedEvent（商品域重算评分摘要、消息通知域）
        RegisterHandler<ReviewModeratedDomainEvent, ReviewModeratedEvent>(e =>
            new ReviewModeratedEvent(e.ReviewId, e.Status, e.Action));
    }
}
