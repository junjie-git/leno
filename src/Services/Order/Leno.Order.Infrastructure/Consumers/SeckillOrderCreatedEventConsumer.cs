using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Order.Application.Services;
using Leno.Promotion.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 消费 Promotion 域发布的 SeckillOrderCreatedEvent，触发秒杀订单创建。
/// </summary>
public sealed class SeckillOrderCreatedEventConsumer : IntegrationEventConsumerBase<SeckillOrderCreatedEvent>
{
    private readonly SeckillOrderCreationService _creationService;

    public SeckillOrderCreatedEventConsumer(
        SeckillOrderCreationService creationService,
        ILogger<SeckillOrderCreatedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(creationService);
        _creationService = creationService;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(SeckillOrderCreatedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        await _creationService.CreateSeckillOrderAsync(integrationEvent, ct);
    }
}
