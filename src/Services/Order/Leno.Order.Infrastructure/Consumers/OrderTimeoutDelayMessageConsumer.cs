using Leno.Infrastructure.Abstractions;
using Leno.Order.Application.Messages;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 订单超时延迟消息消费者，检查待支付订单是否已超时，超时则自动取消。
/// 非 IntegrationEventConsumerBase，因 OrderTimeoutMessage 不是 IIntegrationEvent，
/// 故通过 <see cref="IIdempotencyStore"/> 基于 <c>order-timeout-{OrderId}</c> 幂等键独立去重，
/// 避免延迟消息重投递导致重复取消与重复释放库存/积分/优惠券。
/// </summary>
public sealed class OrderTimeoutDelayMessageConsumer : IConsumer<OrderTimeoutMessage>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockReservationDomainService _stockService;
    private readonly IPointsAntiCorruptionService _pointsAntiCorruption;
    private readonly IPromotionAntiCorruptionService _promotionAntiCorruption;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<OrderTimeoutDelayMessageConsumer> _logger;

    public OrderTimeoutDelayMessageConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IStockReservationDomainService stockService,
        IPointsAntiCorruptionService pointsAntiCorruption,
        IPromotionAntiCorruptionService promotionAntiCorruption,
        IIdempotencyStore idempotencyStore,
        ILogger<OrderTimeoutDelayMessageConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(stockService);
        ArgumentNullException.ThrowIfNull(pointsAntiCorruption);
        ArgumentNullException.ThrowIfNull(promotionAntiCorruption);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(logger);
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _stockService = stockService;
        _pointsAntiCorruption = pointsAntiCorruption;
        _promotionAntiCorruption = promotionAntiCorruption;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderTimeoutMessage> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var msg = context.Message;
        var ct = context.CancellationToken;

        // 幂等键：基于 OrderId 唯一标识超时取消操作，避免延迟消息重投递导致重复取消。
        var idempotencyId = IdempotencyKeyHelper.ToDeterministicGuid($"order-timeout-{msg.OrderId}");

        if (await _idempotencyStore.IsProcessedAsync(idempotencyId, ct))
        {
            _logger.LogInformation("超时取消已处理，跳过重复消费 OrderId={OrderId}", msg.OrderId);
            return;
        }

        // 原子获取处理权（若 store 支持 SET NX），消除并发穿透
        if (_idempotencyStore.SupportsAtomicProcessing
            && !await _idempotencyStore.TryMarkAsProcessingAsync(idempotencyId, ct))
        {
            _logger.LogInformation("超时取消被其他消费者占用或已处理，跳过 OrderId={OrderId}", msg.OrderId);
            return;
        }

        try
        {
            await CancelOrderIfTimeoutAsync(msg, ct);
        }
        catch
        {
            // 处理失败：释放处理锁，允许 MassTransit 后续重试
            if (_idempotencyStore.SupportsAtomicProcessing)
            {
                await _idempotencyStore.ReleaseProcessingLockAsync(idempotencyId, ct);
            }
            throw;
        }

        await _idempotencyStore.MarkAsProcessedAsync(idempotencyId, ct);
        _logger.LogInformation("订单 {OrderId} 超时取消处理完成", msg.OrderId);
    }

    /// <summary>
    /// 加载订单并校验超时条件后执行取消：先持久化订单状态变更（含 OrderCancelledDomainEvent 经 Outbox），
    /// 再释放预占库存、冻结积分与优惠券（可独立重试）。
    /// </summary>
    private async Task CancelOrderIfTimeoutAsync(OrderTimeoutMessage msg, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(msg.OrderId, ct);
        if (order is null)
        {
            _logger.LogInformation("超时取消：订单不存在 OrderId={OrderId}，跳过", msg.OrderId);
            return;
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            _logger.LogInformation("超时取消：订单 {OrderId} 当前状态 {Status} 非待支付，跳过",
                msg.OrderId, order.Status);
            return;
        }

        if (DateTime.UtcNow < order.ExpireAt)
        {
            _logger.LogInformation("超时取消：订单 {OrderId} 尚未到达支付截止时间 {ExpireAt}，跳过",
                msg.OrderId, order.ExpireAt);
            return;
        }

        order.Cancel("支付超时自动取消", "System");

        // 先持久化订单状态变更与 OrderCancelledDomainEvent（经 Outbox 同事务），避免 SaveEntitiesAsync
        // 失败后库存/积分/优惠券已释放但订单状态未变更的不一致
        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 持久化成功后再释放预占库存、冻结积分与优惠券（可独立重试）
        var skuQuantities = order.Items
            .GroupBy(i => i.SkuId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        await _stockService.ReleaseBatchAsync(order.Id, skuQuantities, ct);
        await _pointsAntiCorruption.ReleaseAsync(order.Id, ct);
        await _promotionAntiCorruption.ReleaseCouponsAsync(order.Id, ct);

        _logger.LogInformation("订单 {OrderId} 因支付超时已自动取消", msg.OrderId);
    }
}
