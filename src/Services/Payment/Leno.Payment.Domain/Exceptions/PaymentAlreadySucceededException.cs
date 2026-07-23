using Leno.SharedKernel.Exceptions;

namespace Leno.Payment.Domain.Exceptions;

/// <summary>
/// 订单已由某支付单完成支付后，再次收到该订单的支付请求时抛出。
/// 用于暴露上游（订单域）对已支付订单重复发起支付的缺陷，拒绝重复发起。
/// </summary>
public sealed class PaymentAlreadySucceededException : DomainException
{
    /// <summary>已完成支付的订单标识。</summary>
    public Guid OrderId { get; }

    /// <summary>已完成支付的支付单标识。</summary>
    public Guid PaymentId { get; }

    public PaymentAlreadySucceededException(Guid orderId, Guid paymentId)
        : base($"订单 {orderId} 已由支付单 {paymentId} 完成支付，不可重复发起支付", "PAYMENT_ALREADY_SUCCEEDED")
    {
        OrderId = orderId;
        PaymentId = paymentId;
    }
}
