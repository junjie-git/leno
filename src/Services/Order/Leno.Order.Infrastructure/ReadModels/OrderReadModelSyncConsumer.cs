using Leno.Infrastructure.ReadModel;
using Leno.Order.Domain.Repositories;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.ReadModels;

/// <summary>
/// 订单读模型同步消费者，实现多个 IConsumer&lt;T&gt; 接口，
/// 在订单生命周期事件（创建/支付/发货/完成/取消）时将订单聚合同步索引到 Elasticsearch。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列。
/// </summary>
public sealed class OrderReadModelSyncConsumer :
    IConsumer<OrderCreatedEvent>,
    IConsumer<OrderPaidEvent>,
    IConsumer<OrderShippedEvent>,
    IConsumer<OrderCompletedEvent>,
    IConsumer<OrderCancelledEvent>,
    IConsumer<OrderAfterSalesWindowClosedEvent>
{
    private const string IndexName = "orders";

    private readonly IOrderRepository _orderRepository;
    private readonly IEsReadModelRepository<OrderReadModel> _repository;
    private readonly ILogger<OrderReadModelSyncConsumer> _logger;

    public OrderReadModelSyncConsumer(
        IOrderRepository orderRepository,
        IEsReadModelRepository<OrderReadModel> repository,
        ILogger<OrderReadModelSyncConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _orderRepository = orderRepository;
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SyncAsync(context.Message.OrderId, nameof(OrderCreatedEvent), context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderPaidEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SyncAsync(context.Message.OrderId, nameof(OrderPaidEvent), context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderShippedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SyncAsync(context.Message.OrderId, nameof(OrderShippedEvent), context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SyncAsync(context.Message.OrderId, nameof(OrderCompletedEvent), context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SyncAsync(context.Message.OrderId, nameof(OrderCancelledEvent), context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderAfterSalesWindowClosedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SyncAsync(context.Message.OrderId, nameof(OrderAfterSalesWindowClosedEvent), context.CancellationToken);
    }

    /// <summary>
    /// 加载订单聚合并同步索引到 ES。
    /// </summary>
    private async Task SyncAsync(Guid orderId, string eventType, CancellationToken ct)
    {
        var readModel = await BuildReadModelAsync(orderId, ct);
        if (readModel is null)
        {
            _logger.LogWarning("订单读模型同步跳过：订单不存在 OrderId={OrderId} Event={EventType}",
                orderId, eventType);
            return;
        }

        var success = await _repository.IndexAsync(readModel, orderId.ToString(), IndexName, ct);
        if (!success)
        {
            throw new InvalidOperationException($"ES 读模型索引失败 Id={orderId} Index={IndexName}");
        }

        _logger.LogInformation("订单读模型已同步 OrderId={OrderId} Event={EventType} Index={Index}",
            orderId, eventType, IndexName);
    }

    /// <summary>
    /// 加载订单聚合并映射为读模型文档。
    /// </summary>
    private async Task<OrderReadModel?> BuildReadModelAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            return null;
        }

        var items = order.Items
            .Select(i => new OrderReadModel.OrderItemReadModel
            {
                SkuId = i.SkuId.ToString(),
                ProductName = i.ProductSnapshot.ProductName,
                SkuName = i.ProductSnapshot.SkuName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                Subtotal = i.Subtotal
            })
            .ToList();

        return new OrderReadModel
        {
            OrderId = order.Id.ToString(),
            OrderNo = order.OrderNo,
            UserId = order.UserId.ToString(),
            SellerId = order.SellerId?.ToString() ?? string.Empty,
            Status = order.Status.ToString(),
            OrderType = order.OrderType.ToString(),
            ItemsAmount = order.ItemsAmount,
            DiscountAmount = order.DiscountAmount,
            PointsOffsetAmount = order.PointsOffsetAmount,
            FreightAmount = order.FreightAmount,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            PaidAt = order.PaidAt,
            ShippedAt = order.ShippedAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            Items = items
        };
    }
}
