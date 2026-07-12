using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Repositories;

/// <summary>
/// SPU 仓储 EF Core 实现。
/// 单实体查询带跟踪并预加载 SKU 集合（写场景依赖变更跟踪）；<see cref="QueryAsync"/> 为只读分页。
/// </summary>
public sealed class EfCoreSPURepository : ISPURepository
{
    private readonly ProductDbContext _context;

    public EfCoreSPURepository(ProductDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<SPU?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.SPUs.Include(s => s.SKUs).FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SPU>> GetByShopIdAsync(Guid shopId, CancellationToken ct = default)
    {
        var items = await _context.SPUs
            .Include(s => s.SKUs)
            .Where(s => s.ShopId == shopId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return items;
    }

    /// <inheritdoc />
    public Task<SPU?> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default)
        => _context.SPUs
            .Include(s => s.SKUs)
            .FirstOrDefaultAsync(s => s.SKUs.Any(sk => sk.Id == skuId), ct);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SPU> Items, int Total)> QueryAsync(
        Guid? shopId = null,
        Guid? sellerId = null,
        ProductStatus? status = null,
        Guid? categoryId = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.SPUs.Include(s => s.SKUs).AsNoTracking();

        if (shopId.HasValue)
        {
            query = query.Where(s => s.ShopId == shopId.Value);
        }

        if (sellerId.HasValue)
        {
            query = query.Where(s => s.SellerId == sellerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(s => s.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(s => EF.Functions.Like(s.Title, $"%{kw}%"));
        }

        var total = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public Task AddAsync(SPU aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.SPUs.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(SPU aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.SPUs.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(SPU aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.SPUs.Remove(aggregate);
        return Task.CompletedTask;
    }
}
