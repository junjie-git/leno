using System.Threading.Channels;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Domain.ValueObjects;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 后台 SKU 快照刷新队列（阶段三 3.11）。
/// <para>
/// 基于 <see cref="System.Threading.Channels.Channel{T}"/> 的有界队列 + <see cref="BackgroundService"/> 消费模型。
/// 购物车读取路径检测到快照过期时通过 <see cref="IBackgroundSnapshotRefresher.EnqueueRefresh"/> 入队，
/// 本服务异步消费队列，批量调用商品域防腐层拉取最新快照，更新所有含该 SKU 的购物车项。
/// </para>
/// <para>
/// 刷新流程：
/// 1. 从队列批量取出 skuIds（合并为一次 ACL 调用，减少跨进程调用次数）；
/// 2. 调用 <see cref="IProductSnapshotAntiCorruption.GetSkuSnapshotsAsync"/> 获取最新快照；
/// 3. 经 <see cref="ICartSkuIndexService.GetCartIdsBySkuAsync"/> 定位受影响购物车；
/// 4. 批量加载购物车，调用 <see cref="Leno.Cart.Domain.Aggregates.Cart.UpdateSkuSnapshot"/> 更新快照；
/// 5. 经 UnitOfWork 持久化（每批 100 个购物车提交一次，与 ProductUpdatedEventConsumer 一致）。
/// </para>
/// <para>
/// 幂等性：刷新操作幂等，同一快照重复写入无副作用（SnapshotVersion 递增但价格不变时不发布事件）。
/// 失败处理：ACL 调用失败时记录日志并跳过本次刷新，下次读取过期时重新入队。
/// </para>
/// </summary>
public sealed class SkuSnapshotRefreshQueue : BackgroundService, IBackgroundSnapshotRefresher
{
    private const int CartBatchSize = 100;

    private readonly Channel<Guid> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<CartSnapshotOptions> _options;
    private readonly ILogger<SkuSnapshotRefreshQueue> _logger;

    /// <summary>
    /// 构造函数。由 DI 容器以 Singleton 生命周期注册（BackgroundService 要求）。
    /// 每次刷新操作通过 <see cref="IServiceProvider.CreateScope"/> 创建作用域，
    /// 解析 Scoped 依赖（DbContext、仓储等），避免跨请求共享 DbContext。
    /// </summary>
    public SkuSnapshotRefreshQueue(
        IServiceProvider serviceProvider,
        IOptionsMonitor<CartSnapshotOptions> options,
        ILogger<SkuSnapshotRefreshQueue> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;

        var capacity = Math.Max(1, options.CurrentValue.RefreshQueueCapacity);
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public void EnqueueRefresh(Guid skuId)
    {
        if (skuId == Guid.Empty)
        {
            return;
        }

        // TryWrite 非阻塞：队列满时丢弃最旧（DropOldest 策略）
        _channel.Writer.TryWrite(skuId);
    }

    /// <inheritdoc />
    public void EnqueueRefreshBatch(IEnumerable<Guid> skuIds)
    {
        ArgumentNullException.ThrowIfNull(skuIds);
        foreach (var skuId in skuIds)
        {
            EnqueueRefresh(skuId);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SKU 快照后台刷新队列已启动，并发度={Concurrency} 批量大小={BatchSize}",
            _options.CurrentValue.RefreshConcurrency, _options.CurrentValue.RefreshBatchSize);

        var concurrency = Math.Max(1, _options.CurrentValue.RefreshConcurrency);
        var tasks = new Task[concurrency];
        for (var i = 0; i < concurrency; i++)
        {
            tasks[i] = ConsumeAsync(stoppingToken);
        }

        await Task.WhenAll(tasks);
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var batchSize = Math.Max(1, _options.CurrentValue.RefreshBatchSize);

        await foreach (var batch in ReadBatchAsync(batchSize, stoppingToken))
        {
            try
            {
                await RefreshBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "后台刷新 SKU 快照批量失败，跳过本批 SkuCount={SkuCount}", batch.Count);
            }
        }
    }

    private async IAsyncEnumerable<List<Guid>> ReadBatchAsync(
        int maxBatchSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var batch = new List<Guid>(maxBatchSize);
        while (await _channel.Reader.WaitToReadAsync(ct))
        {
            while (batch.Count < maxBatchSize && _channel.Reader.TryRead(out var skuId))
            {
                // 去重：同一批次内相同 skuId 只保留一个
                if (!batch.Contains(skuId))
                {
                    batch.Add(skuId);
                }
            }

            if (batch.Count > 0)
            {
                yield return batch;
                batch = new List<Guid>(maxBatchSize);
            }
        }
    }

    private async Task RefreshBatchAsync(List<Guid> skuIds, CancellationToken ct)
    {
        if (skuIds.Count == 0)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var snapshotAntiCorruption = scope.ServiceProvider.GetRequiredService<IProductSnapshotAntiCorruption>();
        var indexService = scope.ServiceProvider.GetRequiredService<ICartSkuIndexService>();
        var cartRepository = scope.ServiceProvider.GetRequiredService<ICartRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var dbContext = scope.ServiceProvider.GetRequiredService<CartDbContext>();

        // 1. 批量拉取最新快照
        Dictionary<Guid, SkuSnapshot> snapshots;
        try
        {
            snapshots = await FetchSnapshotsAsync(snapshotAntiCorruption, skuIds, ct);
        }
        catch (AntiCorruptionException ex)
        {
            _logger.LogWarning(ex, "后台批量拉取 SKU 快照失败，跳过本批 SkuCount={SkuCount} ErrorCode={ErrorCode}",
                skuIds.Count, ex.ErrorCode);
            return;
        }

        if (snapshots.Count == 0)
        {
            _logger.LogDebug("后台批量拉取 SKU 快照返回空，跳过 SkuIds={SkuIds}",
                string.Join(",", skuIds));
            return;
        }

        // 2. 逐 SKU 定位受影响购物车并更新快照
        foreach (var (skuId, snapshot) in snapshots)
        {
            await RefreshSingleSkuAsync(skuId, snapshot, indexService, cartRepository, unitOfWork, dbContext, ct);
        }
    }

    private static async Task<Dictionary<Guid, SkuSnapshot>> FetchSnapshotsAsync(
        IProductSnapshotAntiCorruption antiCorruption,
        List<Guid> skuIds,
        CancellationToken ct)
    {
        var dtos = await antiCorruption.GetSkuSnapshotsAsync(skuIds, ct);
        var now = DateTime.UtcNow;
        var result = new Dictionary<Guid, SkuSnapshot>(dtos.Count);
        foreach (var dto in dtos)
        {
            var snapshot = new SkuSnapshot(
                skuId: dto.SkuId,
                skuName: dto.Title,
                price: dto.UnitPrice,
                currency: "CNY",
                mainImageUrl: dto.MainImageUrl,
                specText: null,
                available: dto.IsOnSale,
                snapshotVersion: 1,
                snapshotAt: now);
            result[dto.SkuId] = snapshot;
        }
        return result;
    }

    private async Task RefreshSingleSkuAsync(
        Guid skuId,
        SkuSnapshot snapshot,
        ICartSkuIndexService indexService,
        ICartRepository cartRepository,
        IUnitOfWork unitOfWork,
        CartDbContext dbContext,
        CancellationToken ct)
    {
        var cartIds = await indexService.GetCartIdsBySkuAsync(skuId, ct);
        if (cartIds.Count == 0)
        {
            return;
        }

        foreach (var batch in cartIds.Chunk(CartBatchSize))
        {
            var carts = await cartRepository.GetByIdsAsync(batch, ct);
            foreach (var cart in carts)
            {
                cart.UpdateSkuSnapshot(skuId, snapshot);
            }

            await unitOfWork.SaveEntitiesAsync(ct);
            dbContext.ChangeTracker.Clear();
        }

        _logger.LogDebug("后台刷新 SKU 快照完成 SkuId={SkuId} 更新购物车数={CartCount}",
            skuId, cartIds.Count);
    }
}
