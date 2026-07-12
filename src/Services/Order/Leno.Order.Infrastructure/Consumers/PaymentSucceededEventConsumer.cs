using Leno.Infrastructure.EventBus;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 支付成功事件消费者，将待支付订单标记为已支付。
/// 通过 EventId 幂等去重；订单非待支付态时跳过（已处理或状态非法）。
/// </summary>
public sealed class PaymentSucceededEventConsumer : RedisIntegrationEventConsumerBase<PaymentSucceededEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockReservationDomainService _stockService;
    private readonly IPointsAntiCorruptionService _pointsAntiCorruption;

    public PaymentSucceededEventConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IStockReservationDomainService stockService,
        IPointsAntiCorruptionService pointsAntiCorruption,
        ILogger<PaymentSucceededEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(stockService);
        ArgumentNullException.ThrowIfNull(pointsAntiCorruption);
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _stockService = stockService;
        _pointsAntiCorruption = pointsAntiCorruption;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(PaymentSucceededEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var order = await _orderRepository.GetByIdAsync(integrationEvent.OrderId, ct);
        if (order is null)
        {
            Logger.LogInformation("支付成功事件：订单不存在 OrderId={OrderId}，跳过", integrationEvent.OrderId);
            return;
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            Logger.LogInformation("支付成功事件：订单 {OrderId} 当前状态 {Status} 非待支付，跳过",
                integrationEvent.OrderId, order.Status);
            return;
        }

        order.MarkAsPaid(integrationEvent.PaymentId, integrationEvent.Channel, integrationEvent.PaidAt, integrationEvent.TradeNo);

        // 会员订阅订单支付后自动完成（无发货流程）
        if (order.OrderType == OrderType.Membership)
        {
            order.CompleteMembershipOrder();

            await _orderRepository.UpdateAsync(order, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            Logger.LogInformation("会员订单 {OrderId} 已支付并自动完成", integrationEvent.OrderId);
            return;
        }

        // 确认库存扣减（预占 → 真实扣减）
        var skuQuantities = order.Items
            .GroupBy(i => i.SkuId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        await _stockService.ConfirmBatchAsync(order.Id, skuQuantities, ct);

        // 确认积分扣减（冻结 → 正式扣减）
        await _pointsAntiCorruption.ConfirmDeductionAsync(order.Id, ct);

        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 已标记支付成功 PaymentId={PaymentId} TradeNo={TradeNo}",
            integrationEvent.OrderId, integrationEvent.PaymentId, integrationEvent.TradeNo);
    }
}
