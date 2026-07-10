using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.ReviewAfterSales.Infrastructure.Consumers;

/// <summary>
/// 订单完成事件消费者，开通该订单的评价入口。
/// 本域不维护单独的"可评价订单行"表，此消费者仅记录订单完成事实；
/// 实际评价资格校验由 <c>IReviewEligibilityChecker</c> 在评价提交时通过订单域防腐层执行。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class OrderCompletedEventConsumer : RedisIntegrationEventConsumerBase<OrderCompletedEvent>
{
    public OrderCompletedEventConsumer(
        ILogger<OrderCompletedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
    }

    /// <inheritdoc />
    protected override Task HandleAsync(OrderCompletedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        Logger.LogInformation("订单已完成，评价入口已开通 OrderId={OrderId} UserId={UserId} CompletedAt={CompletedAt}",
            integrationEvent.OrderId, integrationEvent.UserId, integrationEvent.CompletedAt);

        return Task.CompletedTask;
    }
}
