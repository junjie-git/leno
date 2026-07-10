using Leno.Cart.Domain.Repositories;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Consumers;

/// <summary>
/// 订单创建集成事件消费者，订单创建后清空购物车已结算项。
/// 通过 EventId 幂等去重，重复消费不重复清空。
/// </summary>
public sealed class OrderCreatedEventConsumer : RedisIntegrationEventConsumerBase<OrderCreatedEvent>
{
    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderCreatedEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderCreatedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderCreatedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.SourceCartItemIds.Count == 0)
        {
            Logger.LogInformation("订单 {OrderId} 无来源购物车项，跳过清空", integrationEvent.OrderId);
            return;
        }

        var cart = await _cartRepository.GetByUserIdAsync(integrationEvent.BuyerId, ct);
        if (cart is null)
        {
            Logger.LogInformation("买家 {BuyerId} 购物车不存在，跳过清空", integrationEvent.BuyerId);
            return;
        }

        cart.ClearItemsBySourceIds(integrationEvent.SourceCartItemIds);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 已清空购物车 {CartId} 的 {Count} 个已结算项",
            integrationEvent.OrderId, cart.Id, integrationEvent.SourceCartItemIds.Count);
    }
}
