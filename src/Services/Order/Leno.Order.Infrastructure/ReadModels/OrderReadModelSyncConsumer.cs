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
/// 3.12：实现 <see cref="IReadModelProjector{TReadModel}"/>，支持快照重建与增量回放。
/// </summary>
public sealed class OrderReadModelSyncConsumer :
    IConsumer<OrderCreatedEvent>,
    IConsumer<OrderPaidEvent>,
    IConsumer<OrderShippedEvent>,
    IConsumer<OrderCompletedEvent>,
    IConsumer<OrderCancelledEvent>,
    IConsumer<OrderAfterSalesWindowClosedEvent>,
    IReadModelProjector<OrderReadModel>
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
    /// 加载订单聚合并同步索引到 ES（实时增量同步路径）。
    /// 保留读模型已投影的 <see cref="OrderReadModel.Version"/>（若 ES 已存在），避免实时更新回退版本号。
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

        // 保留 ES 中已记录的版本号（实时同步不推进版本，版本由重建路径精确维护）
        var existing = await _repository.GetByIdAsync(orderId.ToString(), IndexName, ct);
        readModel.Version = existing?.Version ?? 0;
        if (existing?.LastSnapshotAt is not null)
        {
            readModel.LastSnapshotAt = existing.LastSnapshotAt;
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

    /// <summary>
    /// 将领域事件投影到订单读模型（重建路径）：加载聚合、设置 <see cref="OrderReadModel.Version"/>、索引到 ES。
    /// </summary>
    public async Task ProjectAsync(DomainEventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!Guid.TryParse(envelope.AggregateId, out var orderId))
        {
            _logger.LogWarning("订单读模型投影跳过：聚合标识非法 AggregateId={AggregateId}", envelope.AggregateId);
            return;
        }

        var readModel = await BuildReadModelAsync(orderId, ct);
        if (readModel is null)
        {
            _logger.LogWarning("订单读模型投影跳过：订单不存在 OrderId={OrderId} Version={Version}",
                orderId, envelope.Version);
            return;
        }

        // 保留已有快照时间，推进版本号到当前事件版本
        var existing = await _repository.GetByIdAsync(orderId.ToString(), IndexName, ct);
        readModel.Version = envelope.Version;
        readModel.LastSnapshotAt = existing?.LastSnapshotAt;

        var success = await _repository.IndexAsync(readModel, orderId.ToString(), IndexName, ct);
        if (!success)
        {
            throw new InvalidOperationException(
                $"ES 读模型投影失败 Id={orderId} Index={IndexName} Version={envelope.Version}");
        }

        _logger.LogDebug("订单读模型已投影 OrderId={OrderId} Version={Version} EventType={EventType}",
            orderId, envelope.Version, envelope.EventType);
    }

    /// <summary>
    /// 从快照恢复订单读模型：直接将快照状态索引到 ES，无需加载聚合（快照重建性能收益的核心）。
    /// </summary>
    public async Task RebuildFromSnapshotAsync(Snapshot<OrderReadModel> snapshot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.State);

        if (!Guid.TryParse(snapshot.AggregateId, out var orderId))
        {
            _logger.LogWarning("订单读模型快照恢复跳过：聚合标识非法 AggregateId={AggregateId}",
                snapshot.AggregateId);
            return;
        }

        // 快照状态已包含 Version，标记快照时间
        snapshot.State.LastSnapshotAt = snapshot.TakenAt;

        var success = await _repository.IndexAsync(snapshot.State, orderId.ToString(), IndexName, ct);
        if (!success)
        {
            throw new InvalidOperationException(
                $"ES 读模型快照恢复失败 Id={orderId} Index={IndexName} Version={snapshot.Version}");
        }

        _logger.LogInformation("订单读模型已从快照恢复 OrderId={OrderId} Version={Version}",
            orderId, snapshot.Version);
    }

    /// <summary>
    /// 获取订单读模型当前已投影的最后事件版本号。读模型不存在时返回 0。
    /// </summary>
    public async Task<long> GetLastProjectedVersionAsync(string aggregateId, CancellationToken ct)
    {
        var current = await GetCurrentStateAsync(aggregateId, ct);
        return current?.Version ?? 0;
    }

    /// <summary>
    /// 获取订单读模型当前状态。读模型不存在时返回 null。
    /// </summary>
    public async Task<OrderReadModel?> GetCurrentStateAsync(string aggregateId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateId);
        return await _repository.GetByIdAsync(aggregateId, IndexName, ct);
    }
}
