using Leno.Inventory.Domain.Aggregates;
using Leno.Inventory.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Inventory.Infrastructure.Repositories;

/// <summary>
/// 库存基线仓储 EF Core 实现。
/// <para>
/// 预占/确认/释放/归还四个高频操作以 <see cref="ExecutableExtensions.ExecuteUpdateAsync"/> 的
/// **单语句条件 UPDATE** 落地（守卫条件写进 WHERE，受影响行数为 0 即守卫不满足）——
/// 这是计数器语义下并发正确的标准做法：两条并发预占同一 SKU，必有一生一败，不存在丢失更新。
/// 语句在应用服务开启的工作单元事务内执行，与台账写入同事务提交。
/// </para>
/// </summary>
public sealed class EfCoreStockBaselineRepository : IStockBaselineRepository
{
    private readonly InventoryDbContext _context;

    public EfCoreStockBaselineRepository(InventoryDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<StockBaseline?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.StockBaselines.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public Task<StockBaseline?> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default)
        => _context.StockBaselines.FirstOrDefaultAsync(s => s.SkuId == skuId, ct);

    /// <inheritdoc />
    public Task AddAsync(StockBaseline aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.StockBaselines.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(StockBaseline aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.StockBaselines.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(StockBaseline aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.StockBaselines.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> TryReserveAsync(Guid skuId, int quantity, CancellationToken ct = default)
        => _context.StockBaselines
            .Where(b => b.SkuId == skuId && b.AvailableQty - b.ReservedQty >= quantity)
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.ReservedQty, b => b.ReservedQty + quantity), ct);

    /// <inheritdoc />
    public Task<int> TryConfirmAsync(Guid skuId, int quantity, CancellationToken ct = default)
        => _context.StockBaselines
            .Where(b => b.SkuId == skuId && b.ReservedQty >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.AvailableQty, b => b.AvailableQty - quantity)
                .SetProperty(b => b.ReservedQty, b => b.ReservedQty - quantity)
                .SetProperty(b => b.DeductedQty, b => b.DeductedQty + quantity), ct);

    /// <inheritdoc />
    public Task<int> TryReleaseAsync(Guid skuId, int quantity, CancellationToken ct = default)
        => _context.StockBaselines
            .Where(b => b.SkuId == skuId && b.ReservedQty >= quantity)
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.ReservedQty, b => b.ReservedQty - quantity), ct);

    /// <inheritdoc />
    public Task<int> TryReturnAsync(Guid skuId, int quantity, CancellationToken ct = default)
        => _context.StockBaselines
            .Where(b => b.SkuId == skuId && b.DeductedQty >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.AvailableQty, b => b.AvailableQty + quantity)
                .SetProperty(b => b.DeductedQty, b => b.DeductedQty - quantity), ct);

    /// <inheritdoc />
    public async Task<int> GetSellableAsync(Guid skuId, CancellationToken ct = default)
    {
        var baseline = await GetBySkuIdAsync(skuId, ct).ConfigureAwait(false);
        return baseline is null ? 0 : baseline.AvailableQty - baseline.ReservedQty;
    }

    /// <inheritdoc />
    public async Task SetAvailableAsync(Guid skuId, Guid productId, int availableQty, CancellationToken ct = default)
    {
        // 低频路径（商品域调整库存事件），走聚合加载-修改-保存即可；
        // 争用路径（预占/确认/释放/归还）才需要原子 UPDATE
        var baseline = await GetBySkuIdAsync(skuId, ct).ConfigureAwait(false);
        if (baseline is null)
        {
            baseline = StockBaseline.Create(Guid.NewGuid(), skuId, availableQty, productId);
            await AddAsync(baseline, ct).ConfigureAwait(false);
            return;
        }

        baseline.ApplyBaselineSync(availableQty);
        await UpdateAsync(baseline, ct).ConfigureAwait(false);
    }
}
