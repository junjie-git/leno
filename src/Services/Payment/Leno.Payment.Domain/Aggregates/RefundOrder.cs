using Leno.Payment.Domain.Events;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Payment.Domain.Aggregates;

/// <summary>
/// 退款单聚合根，封装退款金额、渠道与状态机。
/// 状态流转：Refunding → Succeeded；Refunding → Failed。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>RefundId</c>。
/// </summary>
public sealed class RefundOrder : AggregateRoot
{
    /// <summary>商户退款单号（业务可读，全局唯一），传给第三方渠道作为 out_refund_no。</summary>
    public string OutRefundNo { get; private set; } = string.Empty;

    /// <summary>原支付单商户单号（来自原支付单 OutTradeNo），退款时传给第三方渠道作为 out_trade_no。</summary>
    public string OutTradeNo { get; private set; } = string.Empty;

    /// <summary>关联支付单标识。</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; private set; }

    /// <summary>买家账号标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>关联售后单标识。</summary>
    public Guid AfterSalesId { get; private set; }

    /// <summary>退款金额。</summary>
    public decimal RefundAmount { get; private set; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; private set; } = "CNY";

    /// <summary>支付渠道（与原支付单一致）。</summary>
    public PaymentChannel Channel { get; private set; }

    /// <summary>第三方退款单号（渠道返回）。</summary>
    public string? ChannelRefundNo { get; private set; }

    /// <summary>退款单状态。</summary>
    public RefundStatus Status { get; private set; }

    /// <summary>退款到账时间（UTC）。</summary>
    public DateTime? RefundedAt { get; private set; }

    /// <summary>失败原因。</summary>
    public string? FailReason { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private RefundOrder() { }

    private RefundOrder(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验入参合法、生成商户退款单号并置退款中态。
    /// </summary>
    /// <param name="refundId">退款单标识，由应用层生成。</param>
    /// <param name="paymentId">关联支付单标识。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="userId">买家标识。</param>
    /// <param name="afterSalesId">关联售后单标识。</param>
    /// <param name="refundAmount">退款金额，须 &gt; 0。</param>
    /// <param name="currency">币种，为空默认 CNY。</param>
    /// <param name="outTradeNo">原支付单商户单号，退款时传给第三方渠道作为 out_trade_no。</param>
    /// <param name="channel">支付渠道，与原支付单一致。</param>
    public static RefundOrder Create(
        Guid refundId,
        Guid paymentId,
        Guid orderId,
        Guid userId,
        Guid afterSalesId,
        decimal refundAmount,
        string currency,
        string outTradeNo,
        PaymentChannel channel)
    {
        if (refundId == Guid.Empty)
        {
            throw new PaymentDomainException("RefundId 不可为空", "REFUND_ID_EMPTY");
        }

        if (paymentId == Guid.Empty)
        {
            throw new PaymentDomainException("PaymentId 不可为空", "REFUND_PAYMENT_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(outTradeNo))
        {
            throw new PaymentDomainException("原支付单商户单号不可为空", "REFUND_OUT_TRADE_NO_EMPTY");
        }

        if (orderId == Guid.Empty)
        {
            throw new PaymentDomainException("OrderId 不可为空", "REFUND_ORDER_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new PaymentDomainException("UserId 不可为空", "REFUND_USER_EMPTY");
        }

        if (afterSalesId == Guid.Empty)
        {
            throw new PaymentDomainException("AfterSalesId 不可为空", "REFUND_AFTERSALES_EMPTY");
        }

        if (refundAmount <= 0)
        {
            throw new PaymentDomainException("退款金额须大于 0", "REFUND_AMOUNT_INVALID");
        }

        return new RefundOrder(refundId)
        {
            OutRefundNo = $"RFD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100000, 999999)}",
            PaymentId = paymentId,
            OrderId = orderId,
            UserId = userId,
            AfterSalesId = afterSalesId,
            RefundAmount = refundAmount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency,
            OutTradeNo = outTradeNo,
            Channel = channel,
            Status = RefundStatus.Refunding
        };
    }

    /// <summary>
    /// 标记退款成功，校验退款中态，置成功态并发布 <see cref="RefundCompletedDomainEvent"/>（被订单域消费释放预占库存）。
    /// </summary>
    /// <param name="channelRefundNo">第三方退款单号。</param>
    /// <param name="refundedAt">退款到账时间（UTC）。</param>
    public void MarkSucceeded(string channelRefundNo, DateTime refundedAt)
    {
        if (Status != RefundStatus.Refunding)
        {
            throw new PaymentDomainException(
                $"当前状态 {Status} 不可标记退款成功，仅 Refunding 可标记",
                "REFUND_SUCCESS_STATUS_INVALID");
        }

        if (string.IsNullOrWhiteSpace(channelRefundNo))
        {
            throw new PaymentDomainException("第三方退款单号不可为空", "REFUND_CHANNEL_NO_EMPTY");
        }

        Status = RefundStatus.Succeeded;
        ChannelRefundNo = channelRefundNo;
        RefundedAt = refundedAt;
        AddDomainEvent(new RefundCompletedDomainEvent(OrderId, UserId, Id, AfterSalesId, RefundAmount, Currency, refundedAt));
    }

    /// <summary>
    /// 标记退款失败，校验退款中态，置失败态并发布 <see cref="RefundFailedDomainEvent"/>。
    /// </summary>
    /// <param name="reason">失败原因。</param>
    public void MarkFailed(string reason)
    {
        if (Status != RefundStatus.Refunding)
        {
            throw new PaymentDomainException(
                $"当前状态 {Status} 不可标记退款失败，仅 Refunding 可标记",
                "REFUND_FAIL_STATUS_INVALID");
        }

        Status = RefundStatus.Failed;
        FailReason = reason;
        AddDomainEvent(new RefundFailedDomainEvent(Id, OrderId, AfterSalesId, reason, DateTime.UtcNow));
    }
}
