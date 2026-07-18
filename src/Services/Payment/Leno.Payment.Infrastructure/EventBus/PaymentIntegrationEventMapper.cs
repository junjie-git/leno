using Leno.Infrastructure.EventBus;
using Leno.Payment.Domain.Events;
using Leno.SharedContracts.Events;

namespace Leno.Payment.Infrastructure.EventBus;

/// <summary>
/// Payment BC 领域事件到集成事件的翻译器。
/// 将 PaymentOrder/RefundOrder/PaymentChannelConfig 聚合收集的领域事件翻译为 SharedContracts 中的集成事件。
/// </summary>
public class PaymentIntegrationEventMapper : IntegrationEventMapperBase
{
    public PaymentIntegrationEventMapper()
    {
        // PaymentSucceededDomainEvent → PaymentSucceededEvent（订单域标记已支付、促销域核销优惠券、积分与会员域正式扣减冻结积分/开通会员、卖家域通知发货、库存确认真实扣减）
        RegisterHandler<PaymentSucceededDomainEvent, PaymentSucceededEvent>(e =>
            new PaymentSucceededEvent(e.OrderId, e.PaymentId, e.UserId, e.Channel, e.TradeNo, e.Amount, e.Currency, e.PaidAt));

        // PaymentFailedDomainEvent → PaymentFailedEvent（订单域记录失败原因、消息通知域支付失败通知）
        RegisterHandler<PaymentFailedDomainEvent, PaymentFailedEvent>(e =>
            new PaymentFailedEvent(e.OrderId, e.UserId, e.Reason, e.FailedAt));

        // PaymentClosedDomainEvent → PaymentClosedEvent（订单域取消订单释放预占资源）
        RegisterHandler<PaymentClosedDomainEvent, PaymentClosedEvent>(e =>
            new PaymentClosedEvent(e.PaymentId, e.OrderId, e.Reason, e.ClosedAt));

        // RefundCompletedDomainEvent → RefundCompletedEvent（订单域更新订单退款状态、卖家域扣减结算货款、消息通知域退款到账通知）
        // 使用带 afterSalesId 参数的构造重载，便于售后域关联退款单
        RegisterHandler<RefundCompletedDomainEvent, RefundCompletedEvent>(e =>
            new RefundCompletedEvent(e.OrderId, e.UserId, e.RefundId, e.AfterSalesId, e.RefundAmount, e.Currency, e.CompletedAt));

        // RefundFailedDomainEvent → RefundFailedEvent（售后域更新售后单退款状态可重试、消息通知域退款失败通知）
        RegisterHandler<RefundFailedDomainEvent, RefundFailedEvent>(e =>
            new RefundFailedEvent(e.RefundId, e.OrderId, e.AfterSalesId, e.Reason, e.FailedAt));

        // PaymentChannelConfigChangedDomainEvent → PaymentChannelConfigChangedEvent（渠道适配器刷新缓存配置、运维监控配置变更通知）
        RegisterHandler<PaymentChannelConfigChangedDomainEvent, PaymentChannelConfigChangedEvent>(e =>
            new PaymentChannelConfigChangedEvent(e.ConfigId, e.Channel, e.ConfigName, e.ChangeType));
    }
}
