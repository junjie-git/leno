using Leno.Cart.Application.Abstractions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Consumers;

/// <summary>
/// 商品下架事件消费者：经反向索引定位包含受影响 SKU 的购物车，标记对应项无效并取消选中。
/// 幂等：通过 EventId + Redis SET NX 去重，MarkInvalid 幂等。
/// 批处理：每批 100 个购物车提交一次，避免热门 SKU 下架时长时间阻塞。
/// </summary>
public sealed class ProductTakenDownEventConsumer : IntegrationEventConsumerBase<ProductTakenDownEvent>
{
    private const int BatchSize = 100;
    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartSkuIndexService _indexService;

    public ProductTakenDownEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ICartSkuIndexService indexService,
        ILogger<ProductTakenDownEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(indexService);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
        _indexService = indexService;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ProductTakenDownEvent integrationEvent, CancellationToken ct)
    {
        Logger.LogInformation("处理商品下架事件 ProductId={ProductId} SkuCount={SkuCount}",
            integrationEvent.ProductId, integrationEvent.SkuIds.Count);

        foreach (var skuId in integrationEvent.SkuIds)
        {
            var cartIds = await _indexService.GetCartIdsBySkuAsync(skuId, ct);
            if (cartIds.Count == 0) continue;

            foreach (var batch in cartIds.Chunk(BatchSize))
            {
                foreach (var cartId in batch)
                {
                    var cart = await _cartRepository.GetByIdAsync(cartId, ct);
                    if (cart is null) continue;

                    cart.MarkInvalid(skuId, "商品已下架");
                    await _cartRepository.UpdateAsync(cart, ct);
                }

                await _unitOfWork.SaveEntitiesAsync(ct);
            }
        }
    }
}

/// <summary>
/// 商品上架事件消费者：经反向索引定位包含受影响 SKU 的购物车，恢复对应项有效性。
/// 幂等：通过 EventId + Redis SET NX 去重，MarkValid 幂等。
/// 批处理：每批 100 个购物车提交一次。
/// </summary>
public sealed class ProductPublishedEventConsumer : IntegrationEventConsumerBase<ProductPublishedEvent>
{
    private const int BatchSize = 100;
    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartSkuIndexService _indexService;

    public ProductPublishedEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ICartSkuIndexService indexService,
        ILogger<ProductPublishedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(indexService);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
        _indexService = indexService;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ProductPublishedEvent integrationEvent, CancellationToken ct)
    {
        Logger.LogInformation("处理商品上架事件 ProductId={ProductId} SkuCount={SkuCount}",
            integrationEvent.ProductId, integrationEvent.SkuIds.Count);

        foreach (var skuId in integrationEvent.SkuIds)
        {
            var cartIds = await _indexService.GetCartIdsBySkuAsync(skuId, ct);
            if (cartIds.Count == 0) continue;

            foreach (var batch in cartIds.Chunk(BatchSize))
            {
                foreach (var cartId in batch)
                {
                    var cart = await _cartRepository.GetByIdAsync(cartId, ct);
                    if (cart is null) continue;

                    cart.MarkValid(skuId);
                    await _cartRepository.UpdateAsync(cart, ct);
                }

                await _unitOfWork.SaveEntitiesAsync(ct);
            }
        }
    }
}

/// <summary>
/// 商品更新事件消费者：经反向索引定位包含受影响 SKU 的购物车，刷新展示快照（标题、主图）。
/// 先经防腐层查询 SKU 最新快照（每 SKU 一次），再批量刷新购物车项。
/// 幂等：通过 EventId + Redis SET NX 去重，RefreshDisplaySnapshot 幂等。
/// 批处理：每批 100 个购物车提交一次。
/// </summary>
public sealed class ProductUpdatedEventConsumer : IntegrationEventConsumerBase<ProductUpdatedEvent>
{
    private const int BatchSize = 100;
    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartSkuIndexService _indexService;
    private readonly IProductSnapshotAntiCorruption _snapshotAntiCorruption;

    public ProductUpdatedEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ICartSkuIndexService indexService,
        IProductSnapshotAntiCorruption snapshotAntiCorruption,
        ILogger<ProductUpdatedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(indexService);
        ArgumentNullException.ThrowIfNull(snapshotAntiCorruption);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
        _indexService = indexService;
        _snapshotAntiCorruption = snapshotAntiCorruption;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ProductUpdatedEvent integrationEvent, CancellationToken ct)
    {
        Logger.LogInformation("处理商品更新事件 ProductId={ProductId} Title={Title} SkuCount={SkuCount}",
            integrationEvent.ProductId, integrationEvent.Title, integrationEvent.SkuIds.Count);

        foreach (var skuId in integrationEvent.SkuIds)
        {
            var cartIds = await _indexService.GetCartIdsBySkuAsync(skuId, ct);
            if (cartIds.Count == 0) continue;

            // 每 SKU 查询一次快照，避免重复调用商品域
            var snapshot = await _snapshotAntiCorruption.GetSkuSnapshotAsync(skuId, ct);
            if (snapshot is null)
            {
                Logger.LogWarning("SKU 快照查询失败，跳过刷新 SkuId={SkuId}", skuId);
                continue;
            }

            foreach (var batch in cartIds.Chunk(BatchSize))
            {
                foreach (var cartId in batch)
                {
                    var cart = await _cartRepository.GetByIdAsync(cartId, ct);
                    if (cart is null) continue;

                    cart.RefreshDisplaySnapshot(skuId, snapshot.Title, snapshot.MainImageUrl ?? string.Empty);
                    await _cartRepository.UpdateAsync(cart, ct);
                }

                await _unitOfWork.SaveEntitiesAsync(ct);
            }
        }
    }
}
