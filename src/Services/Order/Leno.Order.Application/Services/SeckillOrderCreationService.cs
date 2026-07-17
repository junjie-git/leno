using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.Promotion.Domain.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Services;

/// <summary>
/// 秒杀订单创建服务，消费 SeckillOrderCreatedEvent 后创建 OrderType.Seckill 订单。
/// 复用秒杀事件携带的 OrderId（已由 Promotion 域预占），不重新生成。
/// </summary>
public sealed class SeckillOrderCreationService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IProductAntiCorruptionService _productAntiCorruption;
    private readonly ILogger<SeckillOrderCreationService> _logger;

    public SeckillOrderCreationService(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IOrderNumberGenerator orderNumberGenerator,
        IProductAntiCorruptionService productAntiCorruption,
        ILogger<SeckillOrderCreationService> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _orderNumberGenerator = orderNumberGenerator;
        _productAntiCorruption = productAntiCorruption;
        _logger = logger;
    }

    public async Task CreateSeckillOrderAsync(SeckillOrderCreatedEvent evt, CancellationToken ct = default)
    {
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

            // 2. 构建订单项（秒杀价格，无积分抵现、无优惠券）
            var snapshot = ProductSnapshot.Create(
                skuInfo.SkuId, skuInfo.SpuId, skuInfo.ProductName, skuInfo.SkuName, skuInfo.MainImage, skuInfo.SellerId);
            var orderItem = OrderItem.Create(
                Guid.NewGuid(), evt.SkuId, snapshot, evt.SeckillPrice, evt.Quantity, null);

            // 3. 使用秒杀默认地址（秒杀场景无收货地址，使用占位地址，用户支付后补充）
            var placeholderAddress = AddressSnapshot.Create(
                "待补充", "00000000000", "待补充", "待补充", "待补充", "秒杀订单支付后补充地址");

            // 4. 生成订单号（OrderId 复用秒杀事件中的，确保幂等）
            var orderNo = await _orderNumberGenerator.GenerateAsync(ct);

            var order = OrderAggregate.Create(
                evt.OrderId, orderNo, OrderType.Seckill, evt.UserId, skuInfo.SellerId,
                new List<OrderItem> { orderItem }, placeholderAddress,
                freightAmount: 0m, pointsOffsetAmount: 0m,
                expireAt: DateTime.UtcNow.AddMinutes(10)); // 秒杀订单 10 分钟支付超时

            // 5. 追加秒杀确认回执事件（Outbox 同事务发布）
            order.MarkSeckillOrderCreated(evt.ActivityId);

            await _orderRepository.AddAsync(order, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            _logger.LogInformation("秒杀订单创建成功 OrderId={OrderId} OrderNo={OrderNo} ActivityId={ActivityId}",
                evt.OrderId, orderNo, evt.ActivityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "秒杀订单创建异常 OrderId={OrderId} ActivityId={ActivityId}", evt.OrderId, evt.ActivityId);
            await PublishFailedEventAsync(evt, ex.Message, ct);
            throw;
        }
    }

    private async Task PublishFailedEventAsync(SeckillOrderCreatedEvent evt, string reason, CancellationToken ct)
    {
        // 失败回执通过 IEventBus 发布（无聚合可挂领域事件）
        // 注：此处使用 IEventBus 是合理的，因为失败路径无聚合状态变更需要同事务
        // 实际实现时注入 IEventBus 后发布 SeckillOrderCreationFailedEvent
        _logger.LogWarning("秒杀订单创建失败，发布失败回执 OrderId={OrderId} Reason={Reason}", evt.OrderId, reason);
        await Task.CompletedTask; // 占位，实际注入 IEventBus 后调用 PublishAsync
    }
}
