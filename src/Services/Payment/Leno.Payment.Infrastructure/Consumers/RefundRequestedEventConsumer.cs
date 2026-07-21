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
/// 退款请求事件消费者，售后域发起退款时发布 <see cref="RefundRequestedIntegrationEvent"/>。
/// 消费时创建退款单、调用渠道退款并保存。退款受理成功保持退款中态，由异步通知或补偿任务确认到账。
/// 幂等：同一退款单标识已存在则跳过。
/// </summary>
public sealed class RefundRequestedEventConsumer : IntegrationEventConsumerBase<RefundRequestedIntegrationEvent>
{
    private readonly IRefundOrderRepository _refundOrderRepository;
    private readonly IPaymentOrderRepository _paymentOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentChannelFactory _channelFactory;

    public RefundRequestedEventConsumer(
        IRefundOrderRepository refundOrderRepository,
        IPaymentOrderRepository paymentOrderRepository,
        IUnitOfWork unitOfWork,
        IPaymentChannelFactory channelFactory,
        ILogger<RefundRequestedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _refundOrderRepository = refundOrderRepository ?? throw new ArgumentNullException(nameof(refundOrderRepository));
        _paymentOrderRepository = paymentOrderRepository ?? throw new ArgumentNullException(nameof(paymentOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(RefundRequestedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 幂等：同一退款单已存在则跳过
        var existing = await _refundOrderRepository.GetByIdAsync(integrationEvent.RefundId, ct);
        if (existing is not null)
        {
            Logger.LogInformation("退款请求事件：退款单已存在 RefundId={RefundId}，跳过", integrationEvent.RefundId);
            return;
        }

        if (!Enum.TryParse(integrationEvent.Channel, true, out PaymentChannel channel))
        {
            Logger.LogWarning("退款请求事件：不支持的支付渠道 Channel={Channel} RefundId={RefundId}，跳过",
                integrationEvent.Channel, integrationEvent.RefundId);
            return;
        }

        var originalPayment = await _paymentOrderRepository.GetByIdAsync(integrationEvent.PaymentId, ct);
        if (originalPayment is null)
        {
            throw new InvalidOperationException($"原支付单不存在 PaymentId={integrationEvent.PaymentId}");
        }

        // 状态校验：仅当原支付单状态为 Paid 时才允许发起退款
        // 若原支付单处于 Pending/ChannelOrdered/Failed/Closed 等状态，渠道侧会拒绝退款请求，
        // 系统不应创建退款单，避免本地与渠道状态不一致
        if (originalPayment.Status != PaymentStatus.Paid)
        {
            throw new InvalidOperationException(
                $"原支付单状态非已支付，不可退款 PaymentId={integrationEvent.PaymentId} Status={originalPayment.Status}");
        }

        var refundOrder = RefundOrder.Create(
            integrationEvent.RefundId,
            integrationEvent.PaymentId,
            integrationEvent.OrderId,
            integrationEvent.UserId,
            integrationEvent.AfterSalesId,
            integrationEvent.RefundAmount,
            integrationEvent.Currency,
            originalPayment.OutTradeNo,
            channel);

        var adapter = _channelFactory.GetAdapter(channel);
        var result = await adapter.CreateRefundAsync(refundOrder, ct);

        if (!result.Succeeded)
        {
            refundOrder.MarkFailed("渠道退款受理失败");
        }

        await _refundOrderRepository.AddAsync(refundOrder, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("退款单已创建 RefundId={RefundId} OutRefundNo={OutRefundNo} Channel={Channel} Succeeded={Succeeded}",
            integrationEvent.RefundId, refundOrder.OutRefundNo, channel, result.Succeeded);
    }
}
