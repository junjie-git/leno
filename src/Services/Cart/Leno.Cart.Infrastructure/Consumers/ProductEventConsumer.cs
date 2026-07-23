using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Domain.ValueObjects;
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

/// <summary>
/// 商品 SKU 更新事件消费者（阶段三 3.11）：消费商品域发布的 <see cref="ProductSkuUpdatedEvent"/>，
/// 直接基于事件携带的 SKU 快照数据更新购物车本地快照，无需回调商品域 ACL。
/// <para>
/// 与 <see cref="ProductUpdatedEventConsumer"/> 区别：后者仅携带商品级标题与主图（粗粒度），
/// 需回调 ACL 获取 SKU 级价格；本事件携带完整 SKU 级数据（价格/币种/规格/可售状态），
/// 是阶段三 3.11 快照本地化的主刷新路径，事件驱动实时性优于后台过期刷新。
/// </para>
/// <para>
/// 幂等：通过 EventId + <see cref="IIdempotencyStore"/> 去重（基类保证）；
/// <see cref="CartAggregate.UpdateSkuSnapshot"/> 对相同快照重复写入无副作用（价格不变时不发布领域事件）。
/// 批处理：每批 100 个购物车提交一次，与 <see cref="ProductUpdatedEventConsumer"/> 一致。
/// </para>
/// </summary>
/// <remarks>
/// P2-9：每批 <see cref="IUnitOfWork.SaveEntitiesAsync"/> 后调用 <c>ChangeTracker.Clear()</c> 清理跟踪，
/// 避免跨批次累积跟踪导致内存增长与变更检测开销。
/// </remarks>
public sealed class ProductSkuUpdatedEventConsumer : IntegrationEventConsumerBase<ProductSkuUpdatedEvent>
{
    private const int BatchSize = 100;

    private readonly ICartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartSkuIndexService _indexService;
    private readonly CartDbContext _dbContext;

    public ProductSkuUpdatedEventConsumer(
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        ICartSkuIndexService indexService,
        CartDbContext dbContext,
        ILogger<ProductSkuUpdatedEventConsumer> logger,
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
    protected override async Task HandleAsync(ProductSkuUpdatedEvent integrationEvent, CancellationToken ct)
    {
        Logger.LogInformation(
            "处理商品 SKU 更新事件 ProductId={ProductId} SkuId={SkuId} Price={Price} Currency={Currency} Available={Available} UpdatedAt={UpdatedAt}",
            integrationEvent.ProductId,
            integrationEvent.SkuId,
            integrationEvent.Price,
            integrationEvent.Currency,
            integrationEvent.Available,
            integrationEvent.UpdatedAt);

        // 事件携带完整 SKU 快照数据，直接构造本地快照，无需回调商品域 ACL
        var snapshot = MapEventToSnapshot(integrationEvent);

        // 经反向索引定位包含该 SKU 的所有购物车
        var cartIds = await _indexService.GetCartIdsBySkuAsync(integrationEvent.SkuId, ct);
        if (cartIds.Count == 0)
        {
            Logger.LogDebug("SKU 未被任何购物车持有，跳过快照更新 SkuId={SkuId}", integrationEvent.SkuId);
            return;
        }

        var updatedCartCount = 0;
        foreach (var batch in cartIds.Chunk(BatchSize))
        {
            // P1-2：批量加载，避免 N+1 SELECT
            var carts = await _cartRepository.GetByIdsAsync(batch, ct);
            foreach (var cart in carts)
            {
                // Cart.UpdateSkuSnapshot 幂等：SkuId 不存在忽略；价格变化时发布 SkuPriceChangedEvent
                cart.UpdateSkuSnapshot(integrationEvent.SkuId, snapshot);
                updatedCartCount++;
            }

            await _unitOfWork.SaveEntitiesAsync(ct);
            // P2-9：清理 ChangeTracker，避免跨批次累积跟踪
            _dbContext.ChangeTracker.Clear();
        }

        Logger.LogInformation(
            "商品 SKU 快照更新完成 SkuId={SkuId} 更新购物车数={CartCount}",
            integrationEvent.SkuId, updatedCartCount);
    }

    /// <summary>
    /// 将集成事件载荷映射为本地 <see cref="SkuSnapshot"/> 值对象。
    /// SnapshotAt 优先取事件 UpdatedAt（商品域时间戳），缺失时回退当前 UTC 时间。
    /// SnapshotVersion 固定为 1：事件为离散更新点，版本号用于后台刷新与事件刷新的并发冲突检测，
    /// 真正的时序判定由 SnapshotAt 时间戳保证。
    /// </summary>
    private static SkuSnapshot MapEventToSnapshot(ProductSkuUpdatedEvent evt) => new(
        skuId: evt.SkuId,
        skuName: evt.SkuName,
        price: evt.Price,
        currency: string.IsNullOrEmpty(evt.Currency) ? "CNY" : evt.Currency,
        mainImageUrl: evt.MainImageUrl,
        specText: evt.SpecText,
        available: evt.Available,
        snapshotVersion: 1,
        snapshotAt: evt.UpdatedAt == default ? DateTime.UtcNow : evt.UpdatedAt);
}
