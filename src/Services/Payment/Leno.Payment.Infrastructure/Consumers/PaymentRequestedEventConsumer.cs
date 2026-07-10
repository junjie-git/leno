using Leno.Infrastructure.EventBus;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Consumers;

/// <summary>
/// 支付请求事件消费者，订单域在待支付订单发起支付时发布 <see cref="PaymentRequestedIntegrationEvent"/>。
/// 消费时创建支付单、调用渠道下单、标记渠道已下单并保存。
/// 幂等：同一订单已存在支付单则跳过。
/// </summary>
public sealed class PaymentRequestedEventConsumer : IntegrationEventConsumerBase<PaymentRequestedIntegrationEvent>
{
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaymentChannelFactory _channelFactory;

    public PaymentRequestedEventConsumer(
        IPaymentOrderRepository paymentOrderRepository,
        IUnitOfWork unitOfWork,
        PaymentChannelFactory channelFactory,
        ILogger<PaymentRequestedEventConsumer> logger)
        : base(logger)
    {
        _paymentOrderRepository = paymentOrderRepository ?? throw new ArgumentNullException(nameof(paymentOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(PaymentRequestedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 幂等：同一订单已存在支付单则跳过
        var existing = await _paymentOrderRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
        if (existing is not null)
        {
            Logger.LogInformation("支付请求事件：订单 {OrderId} 已存在支付单 PaymentId={PaymentId}，跳过",
                integrationEvent.OrderId, existing.Id);
            return;
        }

        if (!Enum.TryParse(integrationEvent.Channel, true, out PaymentChannel channel))
        {
            Logger.LogWarning("支付请求事件：不支持的支付渠道 Channel={Channel} OrderId={OrderId}，跳过",
                integrationEvent.Channel, integrationEvent.OrderId);
            return;
        }

        var paymentOrder = PaymentOrder.Create(
            Guid.NewGuid(),
            integrationEvent.OrderId,
            integrationEvent.UserId,
            integrationEvent.Amount,
            integrationEvent.Currency,
            channel);

        var adapter = _channelFactory.GetAdapter(channel);
        var result = await adapter.CreatePaymentAsync(paymentOrder, ct);

        if (string.IsNullOrEmpty(result.ChannelTradeNo))
        {
            paymentOrder.MarkFailed("渠道下单未返回交易号");
        }
        else
        {
            paymentOrder.MarkChannelOrdered(result.ChannelTradeNo, result.PrepayId, result.CodeUrl, result.H5Url);
        }

        await _paymentOrderRepository.AddAsync(paymentOrder, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("支付单已创建 OrderId={OrderId} PaymentId={PaymentId} OutTradeNo={OutTradeNo} Channel={Channel}",
            integrationEvent.OrderId, paymentOrder.Id, paymentOrder.OutTradeNo, channel);
    }
}
