using Leno.Infrastructure.Abstractions;
using Leno.Order.Application.Abstractions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 库存确认消费者，订阅支付成功事件，仅负责将订单预占库存转为真实扣减（预占 → 真实扣减）。
/// 独立于订单状态变更（<see cref="PaymentSucceededEventConsumer"/>）与积分确认（<see cref="PointsConfirmConsumer"/>）：
/// 通过独立队列（order-stock-confirm）与独立幂等键（stock-confirm-{PaymentId}）实现隔离，
/// 任一操作失败不影响本消费者的执行结果与重试，反之亦然。
/// 会员订阅订单无实物库存，跳过确认（与原 PaymentSucceededEventConsumer 早期返回行为一致）。
/// 3.3：双轨期 feature flag <c>Order:UsePaymentProcessManager</c> 切流。
/// flag=true 时（shadow 模式）：消费者在完成库存确认后调用 <see cref="IOrderPaymentProcessManager.HandleStockConfirmedAsync"/>
/// 转发完成回调给 Process Manager。旧路径仍执行实际工作以保证功能兼容。
/// flag=false 时：仅走旧路径，不调用 Process Manager。
/// </summary>
public sealed class StockConfirmConsumer : IConsumer<PaymentSucceededEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryGateway _inventoryGateway;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<StockConfirmConsumer> _logger;

    public StockConfirmConsumer(
        IOrderRepository orderRepository,
        IInventoryGateway inventoryGateway,
        IIdempotencyStore idempotencyStore,
        ILogger<StockConfirmConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(inventoryGateway);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(logger);

        _orderRepository = orderRepository;
        _inventoryGateway = inventoryGateway;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PaymentSucceededEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        var ct = context.CancellationToken;

        // 幂等键：基于 PaymentId + 操作类型，独立于订单状态消费者与积分消费者的幂等键，
        // 避免重试时重复执行库存确认（Redis 真实扣减不可回滚）。
        var idempotencyId = IdempotencyKeyHelper.ToDeterministicGuid($"stock-confirm-{evt.PaymentId}");

        if (await _idempotencyStore.IsProcessedAsync(idempotencyId, ct))
        {
            _logger.LogInformation("库存确认已处理，跳过重复消费 PaymentId={PaymentId} OrderId={OrderId}",
                evt.PaymentId, evt.OrderId);
            return;
        }

        // 原子获取处理权（若 store 支持 SET NX），消除并发穿透
        if (_idempotencyStore.SupportsAtomicProcessing
            && !await _idempotencyStore.TryMarkAsProcessingAsync(idempotencyId, ct))
        {
            _logger.LogInformation("库存确认被其他消费者占用或已处理，跳过 PaymentId={PaymentId}",
                evt.PaymentId);
            return;
        }
        try
        {
            await ConfirmStockAsync(evt, ct);
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
        _logger.LogInformation("库存确认完成 PaymentId={PaymentId} OrderId={OrderId}",
            evt.PaymentId, evt.OrderId);    }

    /// <summary>
    /// 加载订单并按明细构建 SKU 数量映射，调用库存领域服务确认扣减。
    /// 会员订阅订单无实物库存，跳过。
    /// </summary>
    private async Task ConfirmStockAsync(PaymentSucceededEvent evt, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(evt.OrderId, ct);
        if (order is null)
        {
            _logger.LogInformation("库存确认：订单不存在 OrderId={OrderId}，跳过", evt.OrderId);
            return;
        }

        // 会员订阅订单无实物库存，跳过确认（与原 PaymentSucceededEventConsumer 早期返回行为一致）
        if (order.OrderType == OrderType.Membership)
        {
            _logger.LogDebug("库存确认：会员订单 {OrderId} 跳过库存确认", evt.OrderId);
            return;
        }

        var skuQuantities = order.Items
            .GroupBy(i => i.SkuId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        // 确认扣减已异步化（ConfirmStockCommand）：命令按订单寻址，
        // Inventory 按台账幂等处理，本地的 SKU 数量映射仅供日志核对
        _logger.LogInformation("库存确认：Order={OrderId} Items={Count}（转为异步命令）",
            order.Id, skuQuantities.Count);

        await _inventoryGateway.ConfirmBatchAsync(order.Id, ct);
    }
}
