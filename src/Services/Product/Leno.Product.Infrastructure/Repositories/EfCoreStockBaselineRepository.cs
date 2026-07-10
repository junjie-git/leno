using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Repositories;

/// <summary>
/// 库存基线仓储 EF Core 实现。
/// 按 SKU 标识查询基线，写操作由工作单元统一提交。
/// </summary>
public sealed class EfCoreStockBaselineRepository : IStockBaselineRepository
{
    private readonly ProductDbContext _context;

    public EfCoreStockBaselineRepository(ProductDbContext context)
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
}
