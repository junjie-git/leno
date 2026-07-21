using Leno.Payment.Domain.Repositories;

namespace Leno.Payment.Application.Services;

/// <summary>
/// 支付域内部查询服务实现，供售后域等跨域场景关联支付单信息使用。
/// </summary>
public sealed class PaymentInternalQueryService : IPaymentInternalQueryService
{
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IRefundOrderRepository _refundOrderRepository;

    public PaymentInternalQueryService(
        IPaymentOrderRepository paymentOrderRepository,
        IRefundOrderRepository refundOrderRepository)
    {
        ArgumentNullException.ThrowIfNull(paymentOrderRepository);
        ArgumentNullException.ThrowIfNull(refundOrderRepository);
        _paymentOrderRepository = paymentOrderRepository;
        _refundOrderRepository = refundOrderRepository;
    }

    /// <inheritdoc />
    public async Task<PaymentInfoResultDto?> GetPaymentInfoByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var payment = await _paymentOrderRepository.GetByOrderIdAsync(orderId, ct);
        if (payment is null)
        {
            return null;
        }

        // 查询已成功退款记录，汇总已退款金额
        var successfulRefunds = await _refundOrderRepository.GetSuccessfulRefundsByPaymentIdAsync(payment.Id, ct);
        var refundedAmount = successfulRefunds.Sum(r => r.RefundAmount);

        return new PaymentInfoResultDto
        {
            PaymentId = payment.Id,
            Channel = (int)payment.Channel,
            OrderId = payment.OrderId,
            Status = (int)payment.Status,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaidAt = payment.PaidAt,
            TradeNo = payment.ChannelTradeNo,
            RefundedAmount = refundedAmount
        };
    }
}
