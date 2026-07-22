using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure;
using Leno.Infrastructure.AntiCorruption;
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
/// <remarks>
/// P1-2：使用 <see cref="ICartRepository.GetByIdsAsync"/> 批量加载购物车，替代 foreach + GetByIdAsync 的 N+1 查询；
/// 不再调用 <see cref="ICartRepository.UpdateAsync"/>，依赖 ChangeTracker 自动检测变更。
/// P2-9：每批 <see cref="IUnitOfWork.SaveEntitiesAsync"/> 后调用 <c>ChangeTracker.Clear()</c> 清理跟踪，
/// 避免跨批次累积跟踪导致内存增长与变更检测开销。
/// </remarks>
public sealed class ProductTakenDownEventConsumer : IntegrationEventConsumerBase<ProductTakenDownEvent>
{
    private const int BatchSize = 100;
    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartSkuIndexService _indexService;
    private readonly CartDbContext _dbContext;

    public ProductTakenDownEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ICartSkuIndexService indexService,
        CartDbContext dbContext,
        ILogger<ProductTakenDownEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(indexService);
        ArgumentNullException.ThrowIfNull(dbContext);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
        _indexService = indexService;
        _dbContext = dbContext;
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
                // P1-2：批量加载，避免 N+1 SELECT
                var carts = await _cartRepository.GetByIdsAsync(batch, ct);
                foreach (var cart in carts)
                {
                    cart.MarkInvalid(skuId, "商品已下架");
                    // 不再调用 _cartRepository.UpdateAsync，依赖 ChangeTracker 检测变更
                }

                await _unitOfWork.SaveEntitiesAsync(ct);
                // P2-9：清理 ChangeTracker，避免跨批次累积跟踪
                _dbContext.ChangeTracker.Clear();
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
    private readonly CartDbContext _dbContext;

    public ProductPublishedEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ICartSkuIndexService indexService,
        CartDbContext dbContext,
        ILogger<ProductPublishedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(indexService);
        ArgumentNullException.ThrowIfNull(dbContext);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
        _indexService = indexService;
        _dbContext = dbContext;
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
                var carts = await _cartRepository.GetByIdsAsync(batch, ct);
                foreach (var cart in carts)
                {
                    cart.MarkValid(skuId);
                }

                await _unitOfWork.SaveEntitiesAsync(ct);
                _dbContext.ChangeTracker.Clear();
            }
        }
    }
}

/// <summary>
/// 商品更新事件消费者：经反向索引定位包含受影响 SKU 的购物车，刷新展示快照（标题、主图）。
/// 单事件批量查询 SKU 快照后按 SKU 字典查表，避免每 SKU 一次 HTTP。
/// 幂等：通过 EventId + Redis SET NX 去重，RefreshDisplaySnapshot 幂等。
/// 批处理：每批 100 个购物车提交一次。
/// </summary>
/// <remarks>
/// P1-3：单事件 N SKU 仅 1 次 ACL 调用（GetSkuSnapshotsAsync 批量），替代原 foreach + GetSkuSnapshotAsync 的 N 次 HTTP。
/// P1-2+P2-9：与 ProductTakenDown/ProductPublished 对齐，使用批量加载 + 每批 ChangeTracker.Clear。
/// </remarks>
public sealed class ProductUpdatedEventConsumer : IntegrationEventConsumerBase<ProductUpdatedEvent>
{
    private const int BatchSize = 100;
    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartSkuIndexService _indexService;
    private readonly IProductSnapshotAntiCorruption _snapshotAntiCorruption;
    private readonly CartDbContext _dbContext;

    public ProductUpdatedEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ICartSkuIndexService indexService,
        IProductSnapshotAntiCorruption snapshotAntiCorruption,
        CartDbContext dbContext,
        ILogger<ProductUpdatedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(cartRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(indexService);
        ArgumentNullException.ThrowIfNull(snapshotAntiCorruption);
        ArgumentNullException.ThrowIfNull(dbContext);
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
        _indexService = indexService;
        _snapshotAntiCorruption = snapshotAntiCorruption;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ProductUpdatedEvent integrationEvent, CancellationToken ct)
    {
        Logger.LogInformation("处理商品更新事件 ProductId={ProductId} Title={Title} SkuCount={SkuCount}",
            integrationEvent.ProductId, integrationEvent.Title, integrationEvent.SkuIds.Count);

        // P1-3：单事件仅 1 次 ACL 批量调用，替代原 N 次 foreach + GetSkuSnapshotAsync
        Dictionary<Guid, SkuSnapshotDto> snapshotMap;
        try
        {
            var snapshots = await _snapshotAntiCorruption.GetSkuSnapshotsAsync(integrationEvent.SkuIds, ct);
            snapshotMap = snapshots.ToDictionary(s => s.SkuId);
        }
        catch (AntiCorruptionException ex)
        {
            Logger.LogWarning(ex, "SKU 快照批量查询失败，跳过本次刷新 ProductId={ProductId} ErrorCode={ErrorCode}",
                integrationEvent.ProductId, ex.ErrorCode);
            return;
        }

        foreach (var skuId in integrationEvent.SkuIds)
        {
            if (!snapshotMap.TryGetValue(skuId, out var snapshot))
            {
                // 批量响应未包含该 SKU（不存在或未返回），跳过该 SKU 的刷新
                continue;
            }

            var cartIds = await _indexService.GetCartIdsBySkuAsync(skuId, ct);
            if (cartIds.Count == 0) continue;

            foreach (var batch in cartIds.Chunk(BatchSize))
            {
                var carts = await _cartRepository.GetByIdsAsync(batch, ct);
                foreach (var cart in carts)
                {
                    cart.RefreshDisplaySnapshot(skuId, snapshot.Title, snapshot.MainImageUrl ?? string.Empty);
                }

                await _unitOfWork.SaveEntitiesAsync(ct);
                _dbContext.ChangeTracker.Clear();
            }
        }
    }
}
