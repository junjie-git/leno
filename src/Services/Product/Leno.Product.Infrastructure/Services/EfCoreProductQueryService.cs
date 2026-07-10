using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Services;
using Leno.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Services;

/// <summary>
/// 商品查询防腐层实现，供订单域等下游上下文查询 SKU 价格与库存。
/// 直接读 SKU 与库存基线表（只读，AsNoTracking），不暴露商品聚合内部结构。
/// </summary>
public sealed class EfCoreProductQueryService : IProductQueryService
{
    private readonly ProductDbContext _context;

    public EfCoreProductQueryService(ProductDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Money?> GetSkuPriceAsync(Guid skuId, CancellationToken ct = default)
    {
        var sku = await _context.Set<SKU>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == skuId, ct);

        return sku?.Price;
    }

    /// <inheritdoc />
    public async Task<int> GetSkuStockAsync(Guid skuId, CancellationToken ct = default)
    {
        var baseline = await _context.StockBaselines
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.SkuId == skuId, ct);

        return baseline is null ? 0 : baseline.AvailableQty - baseline.ReservedQty;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CheckSkusAvailableAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skuIds);

        var baselines = await _context.StockBaselines
            .AsNoTracking()
            .Where(b => skuIds.Contains(b.SkuId))
            .Select(b => new { b.SkuId, Available = b.AvailableQty - b.ReservedQty })
            .ToListAsync(ct);

        return baselines.ToDictionary(b => b.SkuId, b => b.Available);
    }
}
