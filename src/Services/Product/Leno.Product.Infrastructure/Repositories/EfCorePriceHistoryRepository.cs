using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Repositories;

/// <summary>
/// 价格历史仓储 EF Core 实现。
/// 只读查询使用 AsNoTracking；写操作由工作单元统一提交。
/// </summary>
public sealed class EfCorePriceHistoryRepository : IPriceHistoryRepository
{
    private readonly ProductDbContext _context;

    public EfCorePriceHistoryRepository(ProductDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<PriceHistory?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.PriceHistories.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PriceHistory>> GetBySpuIdAsync(Guid spuId, CancellationToken ct = default)
    {
        var items = await _context.PriceHistories
            .AsNoTracking()
            .Where(p => p.SpuId == spuId)
            .OrderByDescending(p => p.ChangedAt)
            .ToListAsync(ct);

        return items;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PriceHistory>> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default)
    {
        var items = await _context.PriceHistories
            .AsNoTracking()
            .Where(p => p.SkuId == skuId)
            .OrderByDescending(p => p.ChangedAt)
            .ToListAsync(ct);

        return items;
    }

    /// <inheritdoc />
    public Task AddAsync(PriceHistory aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.PriceHistories.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(PriceHistory aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.PriceHistories.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(PriceHistory aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.PriceHistories.Remove(aggregate);
        return Task.CompletedTask;
    }
}
