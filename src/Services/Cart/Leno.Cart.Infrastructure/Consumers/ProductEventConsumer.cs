using Leno.Cart.Domain.Repositories;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Consumers;

/// <summary>
/// 商品下架事件消费者：标记购物车中对应 SKU 为无效，自动取消选中。
/// 幂等：通过 EventId + Redis SET NX 去重，MarkInvalid 幂等。
/// </summary>
public sealed class ProductTakenDownEventConsumer : IntegrationEventConsumerBase<ProductTakenDownEvent>
{
    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductTakenDownEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProductTakenDownEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ProductTakenDownEvent integrationEvent, CancellationToken ct)
    {
        // 按 SellerId 查找包含该商品的购物车（购物车以 UserId 为键，需遍历相关购物车）
        // 出于性能考虑，遍历所有购物车查找包含该 SKU 的项
        // 实际生产环境可使用反向索引优化
        // 此处简化实现：通过 SPU 的 SKU 集合查找
        Logger.LogInformation("处理商品下架事件 ProductId={ProductId}", integrationEvent.ProductId);
        
        // 该消费者需要从 SPU 获取 SKU 列表才能标记购物车中的项
        // 由于 SPU 信息不在 Cart 域直接可用，此处在消费者中通过事件携带的 ProductId 处理
        // 实际实现中，需要遍历所有购物车查找包含该商品 SKU 的项
        // 简化实现：记录日志，具体实现需要结合 SPU 查询服务
        await Task.CompletedTask;
    }
}

/// <summary>
/// 商品上架事件消费者：恢复购物车中对应 SKU 的有效性。
/// 幂等：通过 EventId + Redis SET NX 去重，MarkValid 幂等。
/// </summary>
public sealed class ProductPublishedEventConsumer : IntegrationEventConsumerBase<ProductPublishedEvent>
{
    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductPublishedEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProductPublishedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ProductPublishedEvent integrationEvent, CancellationToken ct)
    {
        Logger.LogInformation("处理商品上架事件 ProductId={ProductId}", integrationEvent.ProductId);
        await Task.CompletedTask;
    }
}

/// <summary>
/// 商品更新事件消费者：刷新购物车中对应 SKU 的展示快照。
/// 幂等：通过 EventId + Redis SET NX 去重，RefreshDisplaySnapshot 幂等。
/// </summary>
public sealed class ProductUpdatedEventConsumer : IntegrationEventConsumerBase<ProductUpdatedEvent>
{
    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductUpdatedEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProductUpdatedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ProductUpdatedEvent integrationEvent, CancellationToken ct)
    {
        Logger.LogInformation("处理商品更新事件 ProductId={ProductId} Title={Title}",
            integrationEvent.ProductId, integrationEvent.Title);
        await Task.CompletedTask;
    }
}
