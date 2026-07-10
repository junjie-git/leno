using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SellerShop.Infrastructure.Repositories;

/// <summary>
/// 店铺仓储 EF Core 实现。
/// 单实体查询带跟踪（写场景依赖变更跟踪）；<see cref="QueryAsync"/> 为只读分页，使用 AsNoTracking。
/// </summary>
public sealed class EfCoreShopRepository : IShopRepository
{
    private readonly SellerShopDbContext _context;

    public EfCoreShopRepository(SellerShopDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Shops.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public Task<Shop?> GetBySellerIdAsync(Guid sellerId, CancellationToken ct = default)
        => _context.Shops.FirstOrDefaultAsync(s => s.SellerId == sellerId, ct);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Shop> Items, int Total)> QueryAsync(
        ShopStatus? status = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.Shops.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(s => EF.Functions.Like(s.ShopName, $"%{kw}%"));
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
    public Task AddAsync(Shop aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Shops.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(Shop aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.Shops.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Shop aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Shops.Remove(aggregate);
        return Task.CompletedTask;
    }
}
