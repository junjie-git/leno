using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Services;
using Leno.Cart.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 基于 SKU 快照的购物车价格服务装饰器（阶段三 3.11）。
/// <para>
/// 装饰原始的 <see cref="ICartPriceService"/>（HttpClient 或 gRPC 实时调用），
/// 在 <c>Cart:UseSkuSnapshot=true</c> 时优先读取本地 <see cref="SkuSnapshot"/>：
/// <list type="bullet">
///   <item>快照存在且未过期：直接返回快照价格，零跨进程调用。</item>
///   <item>快照过期但存在：返回过期快照价格（容忍最终一致），同时异步入队后台刷新。</item>
///   <item>快照缺失：回退到原始实时调用 <see cref="ICartPriceService"/>，同时入队后台刷新。</item>
/// </list>
/// </para>
/// <para>
/// feature flag 关闭（<c>UseSkuSnapshot=false</c>）时，所有请求透传给原始服务，保持向后兼容。
/// 后台刷新由 <see cref="IBackgroundSnapshotRefresher"/> 非阻塞入队，不影响读取路径延迟。
/// </para>
/// <para>
/// 本类仅查询 cart_items 表中已存储的快照，不修改快照。快照的写入由
/// <see cref="Consumers.ProductSkuUpdatedEventConsumer"/>（事件驱动）与
/// <see cref="SkuSnapshotRefreshQueue"/>（后台刷新）负责。
/// </para>
/// </summary>
public sealed class SnapshotCartPriceService : ICartPriceService
{
    private readonly ICartPriceService _inner;
    private readonly CartDbContext _dbContext;
    private readonly IBackgroundSnapshotRefresher _backgroundRefresher;
    private readonly IOptionsMonitor<CartSnapshotOptions> _options;
    private readonly ILogger<SnapshotCartPriceService> _logger;

    public SnapshotCartPriceService(
        ICartPriceService inner,
        CartDbContext dbContext,
        IBackgroundSnapshotRefresher backgroundRefresher,
        IOptionsMonitor<CartSnapshotOptions> options,
        ILogger<SnapshotCartPriceService> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(backgroundRefresher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _inner = inner;
        _dbContext = dbContext;
        _backgroundRefresher = backgroundRefresher;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkuPriceSnapshot>> GetSkuPricesAsync(
        IEnumerable<Guid> skuIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skuIds);
        var ids = skuIds.ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<SkuPriceSnapshot>();
        }

        // feature flag 关闭时透传给原始服务
        if (!_options.CurrentValue.UseSkuSnapshot)
        {
            return await _inner.GetSkuPricesAsync(ids, ct);
        }

        var maxAge = _options.CurrentValue.SnapshotMaxAge;
        var results = new Dictionary<Guid, SkuPriceSnapshot>(ids.Count);
        var staleOrMissingSkuIds = new List<Guid>(ids.Count);

        // 查询本地快照：从 cart_items 表加载包含指定 SkuId 且快照非空的记录
        // 同一 SkuId 可能被多个购物车项持有，取 SnapshotAt 最新的一条
        var cartItemsWithSnapshots = await _dbContext.Set<CartItem>()
            .AsNoTracking()
            .Where(i => ids.Contains(i.SkuId) && i.SkuSnapshot != null)
            .ToListAsync(ct);

        var latestSnapshotsBySkuId = cartItemsWithSnapshots
            .GroupBy(i => i.SkuId)
            .Select(g => g.OrderByDescending(i => i.SkuSnapshot!.SnapshotAt).First())
            .ToDictionary(i => i.SkuId);

        foreach (var skuId in ids)
        {
            if (!latestSnapshotsBySkuId.TryGetValue(skuId, out var item) || item.SkuSnapshot is null)
            {
                // 本地无快照：回退实时调用 + 入队后台刷新
                staleOrMissingSkuIds.Add(skuId);
                continue;
            }

            var snapshot = item.SkuSnapshot;
            if (snapshot.IsStale(maxAge))
            {
                // 快照过期：入队后台刷新，但本次仍返回过期快照（容忍最终一致）
                staleOrMissingSkuIds.Add(skuId);
                _logger.LogDebug("SKU 快照已过期，返回过期快照并触发后台刷新 SkuId={SkuId} SnapshotAt={SnapshotAt}",
                    skuId, snapshot.SnapshotAt);
            }

            results[skuId] = MapSnapshotToPriceSnapshot(snapshot, item.SellerId);
        }

        // 非阻塞入队后台刷新（过期 + 缺失）
        if (staleOrMissingSkuIds.Count > 0)
        {
            _backgroundRefresher.EnqueueRefreshBatch(staleOrMissingSkuIds);
        }

        // 仅对本地缺失的 SkuId 回退实时调用（过期快照不回退，避免跨进程调用抵消快照收益）
        var missingSkuIds = ids.Where(id => !results.ContainsKey(id)).ToList();
        if (missingSkuIds.Count > 0)
        {
            await FetchMissingFromInnerAsync(missingSkuIds, results, ct);
        }

        return results.Values.ToList();
    }

    private async Task FetchMissingFromInnerAsync(
        List<Guid> missingSkuIds,
        Dictionary<Guid, SkuPriceSnapshot> results,
        CancellationToken ct)
    {
        try
        {
            var fallbackResults = await _inner.GetSkuPricesAsync(missingSkuIds, ct);
            foreach (var snapshot in fallbackResults)
            {
                results[snapshot.SkuId] = snapshot;
            }

            if (fallbackResults.Count < missingSkuIds.Count)
            {
                var stillMissing = missingSkuIds.Except(fallbackResults.Select(s => s.SkuId)).ToList();
                _logger.LogWarning("实时价格服务返回部分结果，仍有 {Count} 个 SKU 缺失价格 MissingSkuIds={MissingSkuIds}",
                    stillMissing.Count, string.Join(",", stillMissing));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "回退实时价格服务失败，{Count} 个 SKU 缺失价格 MissingSkuIds={MissingSkuIds}",
                missingSkuIds.Count, string.Join(",", missingSkuIds));
            // 不重新抛出：调用方（CartAppService.BuildCartDtoAsync）依据结果中缺失的 SKU
            // 标记 PriceUnavailable，不会产生 0 元结算
        }
    }

    private static SkuPriceSnapshot MapSnapshotToPriceSnapshot(SkuSnapshot snapshot, Guid sellerId) => new()
    {
        SkuId = snapshot.SkuId,
        Price = snapshot.Price,
        Currency = snapshot.Currency,
        Available = snapshot.Available,
        Title = snapshot.SkuName,
        MainImageUrl = snapshot.MainImageUrl ?? string.Empty,
        SellerId = sellerId
    };
}
