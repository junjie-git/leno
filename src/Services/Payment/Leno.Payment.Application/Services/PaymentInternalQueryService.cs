using Leno.Payment.Domain.Repositories;

namespace Leno.Payment.Application.Services;

/// <summary>
/// 支付域内部查询服务实现，供售后域等跨域场景关联支付单信息使用。
/// </summary>
public sealed class PaymentInternalQueryService : IPaymentInternalQueryService
{
    private readonly IPaymentOrderRepository _paymentOrderRepository;

    public PaymentInternalQueryService(IPaymentOrderRepository paymentOrderRepository)
    {
        ArgumentNullException.ThrowIfNull(paymentOrderRepository);
        _paymentOrderRepository = paymentOrderRepository;
    }

    /// <inheritdoc />
    public async Task<PaymentInfoResultDto?> GetPaymentInfoByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var payment = await _paymentOrderRepository.GetByOrderIdAsync(orderId, ct);
        if (payment is null)
        {
            return null;
        }

        return new PaymentInfoResultDto
        {
            PaymentId = payment.Id,
            Channel = (int)payment.Channel,
            OrderId = payment.OrderId,
            Status = (int)payment.Status
        };
    }
}
