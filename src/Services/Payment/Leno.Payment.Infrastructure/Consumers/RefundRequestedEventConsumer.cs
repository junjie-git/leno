using Leno.Infrastructure.EventBus;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Payment.Infrastructure.Consumers;

/// <summary>
/// 退款请求事件消费者，售后域发起退款时发布 <see cref="RefundRequestedIntegrationEvent"/>。
/// 消费时创建退款单、调用渠道退款并保存。退款受理成功保持退款中态，由异步通知或补偿任务确认到账。
/// 幂等：同一退款单标识已存在则跳过。
/// </summary>
public sealed class RefundRequestedEventConsumer : RedisIntegrationEventConsumerBase<RefundRequestedIntegrationEvent>
{
    private readonly IRefundOrderRepository _refundOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaymentChannelFactory _channelFactory;

    public RefundRequestedEventConsumer(
        IRefundOrderRepository refundOrderRepository,
        IUnitOfWork unitOfWork,
        PaymentChannelFactory channelFactory,
        ILogger<RefundRequestedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        _refundOrderRepository = refundOrderRepository ?? throw new ArgumentNullException(nameof(refundOrderRepository));
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

        var refundOrder = RefundOrder.Create(
            integrationEvent.RefundId,
            integrationEvent.PaymentId,
            integrationEvent.OrderId,
            integrationEvent.UserId,
            integrationEvent.AfterSalesId,
            integrationEvent.RefundAmount,
            integrationEvent.Currency,
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
