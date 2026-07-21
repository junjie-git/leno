using Leno.Infrastructure.EventBus;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.Payment.Infrastructure.Consumers;

/// <summary>
/// 支付请求事件消费者，订单域在待支付订单发起支付时发布 <see cref="PaymentRequestedIntegrationEvent"/>。
/// 消费时创建支付单、先持久化支付单（Pending 态）、再调用渠道下单、最后更新状态并保存。
/// 幂等：同一订单已存在支付单则跳过。
/// </summary>
/// <remarks>
/// P0-6 修复：原实现先调渠道下单再保存支付单，渠道下单成功但本地保存失败时支付单丢失，
/// 无法关联回调或对账，造成资金损失。正确顺序为先持久化支付单（Pending 态）再调渠道下单，
/// 即使后续保存失败，支付单已落库可由对账/关单补偿任务处理，且消息重试时被幂等检查跳过。
/// </remarks>
public sealed class PaymentRequestedEventConsumer : IntegrationEventConsumerBase<PaymentRequestedIntegrationEvent>
{
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentChannelFactory _channelFactory;

    public PaymentRequestedEventConsumer(
        IPaymentOrderRepository paymentOrderRepository,
        IUnitOfWork unitOfWork,
        IPaymentChannelFactory channelFactory,
        ILogger<PaymentRequestedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
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

        // 1. 创建支付单（Pending 态）
        var paymentOrder = PaymentOrder.Create(
            Guid.NewGuid(),
            integrationEvent.OrderId,
            integrationEvent.UserId,
            integrationEvent.Amount,
            integrationEvent.Currency,
            channel);

        // 2. 先持久化支付单（Pending 态），确保渠道下单成功后本地有记录可关联，
        //    避免渠道下单成功但本地保存失败时丢单。
        await _paymentOrderRepository.AddAsync(paymentOrder, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 3. 调用渠道下单
        var adapter = _channelFactory.GetAdapter(channel);
        var result = await adapter.CreatePaymentAsync(paymentOrder, ct);

        // 4. 根据渠道返回更新支付单状态
        if (string.IsNullOrEmpty(result.ChannelTradeNo))
        {
            paymentOrder.MarkFailed("渠道下单未返回交易号");
        }
        else
        {
            paymentOrder.MarkChannelOrdered(result.ChannelTradeNo, result.PrepayId, result.CodeUrl, result.H5Url);
        }

        // 5. 保存状态更新
        await _paymentOrderRepository.UpdateAsync(paymentOrder, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("支付单已创建 OrderId={OrderId} PaymentId={PaymentId} OutTradeNo={OutTradeNo} Channel={Channel}",
            integrationEvent.OrderId, paymentOrder.Id, paymentOrder.OutTradeNo, channel);
    }
}
