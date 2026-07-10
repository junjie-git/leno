using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 支付失败事件消费者，仅记录失败日志。
/// 订单保持待支付态，由延迟消息超时取消机制处理。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class PaymentFailedEventConsumer : RedisIntegrationEventConsumerBase<PaymentFailedEvent>
{
    public PaymentFailedEventConsumer(
        ILogger<PaymentFailedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
    }

    /// <inheritdoc />
    protected override Task HandleAsync(PaymentFailedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        Logger.LogWarning("支付失败 OrderId={OrderId} Reason={Reason} FailedAt={FailedAt}，订单保持待支付，等待超时取消",
            integrationEvent.OrderId, integrationEvent.Reason, integrationEvent.FailedAt);

        return Task.CompletedTask;
    }
}
