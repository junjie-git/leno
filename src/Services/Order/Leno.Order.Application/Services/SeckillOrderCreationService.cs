using Leno.Infrastructure.Abstractions;
using Leno.Order.Application.Abstractions;
using Leno.Order.Application.Messages;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Services;

/// <summary>
/// 秒杀订单创建服务，消费 SeckillOrderCreatedIntegrationEvent 后创建 OrderType.Seckill 订单。
/// 复用秒杀事件携带的 OrderId（已由 Promotion 域预占），不重新生成。
/// </summary>
public sealed class SeckillOrderCreationService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IProductAntiCorruptionService _productAntiCorruption;
    private readonly IInventoryGateway _inventoryGateway;
    private readonly IEventBus _eventBus;
    private readonly IMessageScheduler _messageScheduler;
    private readonly ILogger<SeckillOrderCreationService> _logger;

    public SeckillOrderCreationService(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IOrderNumberGenerator orderNumberGenerator,
        IProductAntiCorruptionService productAntiCorruption,
        IInventoryGateway inventoryGateway,
        IEventBus eventBus,
        IMessageScheduler messageScheduler,
        ILogger<SeckillOrderCreationService> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _orderNumberGenerator = orderNumberGenerator;
        _productAntiCorruption = productAntiCorruption;
        _inventoryGateway = inventoryGateway;
        _eventBus = eventBus;
        _messageScheduler = messageScheduler;
        _logger = logger;
    }

    public async Task CreateSeckillOrderAsync(SeckillOrderCreatedIntegrationEvent evt, CancellationToken ct = default)
    {
        // 台账预占是否已建立 —— 决定异常时是否需要释放（避免"无主预占"永久占用库存）
        var reservationCreated = false;
        try
        {
            // 1. 查询 SKU 信息获取卖家与商品快照
            var skuInfo = await _productAntiCorruption.GetSkuInfoAsync(evt.SkuId, ct);
            if (skuInfo is null || !skuInfo.IsOnSale)
            {
                _logger.LogWarning("秒杀下单失败：SKU 不存在或已下架 SkuId={SkuId}", evt.SkuId);
                await PublishFailedEventAsync(evt, "SKU 不存在或已下架", ct);
                return;
            }

            // 2. 预占库存台账（秒杀结算收口，2026-09-24）
            //    Redis 配额只做准入（Promotion 侧高频热路径），库存权威在 Inventory 的 SQL 台账 ——
            //    订单真正落地时必须落台账，否则 Inventory 的可用量会高估"秒杀已售"部分，导致超卖。
            var reserved = await _inventoryGateway
                .ReserveBatchAsync(evt.OrderId, new Dictionary<Guid, int> { [evt.SkuId] = evt.Quantity }, ct)
                .ConfigureAwait(false);
            if (!reserved)
            {
                // 配额已放行但台账不足（如该 SKU 同时被普通订单占用）→ 发失败回执，
                // 由 Promotion 侧回退 Redis 配额与 DB 基线（既有补偿链路）
                _logger.LogWarning(
                    "秒杀下单失败：台账预占不足 OrderId={OrderId} SkuId={SkuId} Quantity={Quantity}",
                    evt.OrderId, evt.SkuId, evt.Quantity);
                await PublishFailedEventAsync(evt, "库存预占失败：SKU 库存不足", ct);
                return;
            }
            reservationCreated = true;

            // 3. 构建订单项（秒杀价格，无积分抵现、无优惠券）
            var snapshot = ProductSnapshot.Create(
                skuInfo.SkuId, skuInfo.SpuId, skuInfo.ProductName, skuInfo.SkuName, skuInfo.MainImage, skuInfo.SellerId);
            var orderItem = OrderItem.Create(
                Guid.NewGuid(), evt.SkuId, snapshot, evt.SeckillPrice, evt.Quantity, null);

            // 4. 使用秒杀默认地址（秒杀场景无收货地址，使用占位地址，用户支付后补充）
            var placeholderAddress = AddressSnapshot.Create(
                "待补充", "00000000000", "待补充", "待补充", "待补充", "秒杀订单支付后补充地址");

            // 5. 生成订单号（OrderId 复用秒杀事件中的，确保幂等）
            var orderNo = await _orderNumberGenerator.GenerateAsync(ct);

            var order = OrderAggregate.Create(
                evt.OrderId, orderNo, OrderType.Seckill, evt.UserId, skuInfo.SellerId,
                new List<OrderItem> { orderItem }, placeholderAddress,
                freightAmount: 0m, pointsOffsetAmount: 0m,
                expireAt: DateTime.UtcNow.AddMinutes(10)); // 秒杀订单 10 分钟支付超时

            // 6. 追加秒杀确认回执事件（Outbox 同事务发布）
            order.MarkSeckillOrderCreated(evt.ActivityId);

            await _orderRepository.AddAsync(order, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            _logger.LogInformation("秒杀订单创建成功 OrderId={OrderId} OrderNo={OrderNo} ActivityId={ActivityId}",
                evt.OrderId, orderNo, evt.ActivityId);
        }
        catch (Exception ex)
        {
            // 台账已预占但订单未落库 → 释放预占（同普通下单的反向补偿），
            // 再发失败回执让 Promotion 回退配额；顺序不可颠倒，否则"台账已放、回执已回"的窗口期会造成口径错位
            if (reservationCreated)
            {
                try
                {
                    await _inventoryGateway.ReleaseBatchAsync(evt.OrderId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception releaseEx)
                {
                    _logger.LogError(releaseEx,
                        "秒杀台账预占释放失败（订单未落库，需人工核对 stock_reservations）OrderId={OrderId}", evt.OrderId);
                }
            }

            _logger.LogError(ex, "秒杀订单创建异常 OrderId={OrderId} ActivityId={ActivityId}", evt.OrderId, evt.ActivityId);
            await PublishFailedEventAsync(evt, ex.Message, ct);
            throw;
        }

        // 7. 调度支付超时延迟消息（与普通下单同机制：Quartz 持久化调度器）
        //    未支付到期 → OrderTimeoutDelayMessageConsumer 取消订单并释放台账预占（ReleaseStockCommand）。
        //    放在 try 之外：此处订单与预占均已提交，调度失败不应触发上面的"释放预占"补偿路径。
        await _messageScheduler.ScheduleSend(
            new Uri("queue:order-timeout"),
            DateTime.UtcNow.AddMinutes(10),
            new OrderTimeoutMessage(evt.OrderId),
            ct).ConfigureAwait(false);
    }

    private async Task PublishFailedEventAsync(SeckillOrderCreatedIntegrationEvent evt, string reason, CancellationToken ct)
    {
        var failedEvent = new SeckillOrderCreationFailedIntegrationEvent(
            evt.ActivityId, evt.SkuId, evt.UserId, evt.OrderId, evt.Quantity, reason);
        try
        {
            await _eventBus.PublishAsync(failedEvent, ct).ConfigureAwait(false);
            _logger.LogWarning("秒杀订单创建失败回执已发布 OrderId={OrderId} Reason={Reason}", evt.OrderId, reason);
        }
        catch (Exception ex)
        {
            // 失败回执发布失败仅记日志，不重抛（避免吞掉原始创建异常）
            _logger.LogError(ex, "秒杀失败回执发布失败 OrderId={OrderId}", evt.OrderId);
        }
    }
}
