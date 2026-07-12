using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SellerShop.Infrastructure.Repositories;

/// <summary>
/// 店铺经营数据仓储 EF Core 实现。
/// 每店铺仅一条记录，按 ShopId 唯一查询与 upsert。
/// </summary>
public sealed class EfCoreShopDashboardRepository : IShopDashboardRepository
{
    private readonly SellerShopDbContext _context;

    public EfCoreShopDashboardRepository(SellerShopDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<ShopDashboardData?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.ShopDashboardData.FirstOrDefaultAsync(d => d.Id == id, ct);

    /// <inheritdoc />
    public Task AddAsync(ShopDashboardData aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.ShopDashboardData.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(ShopDashboardData aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.ShopDashboardData.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(ShopDashboardData aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.ShopDashboardData.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ShopDashboardData?> GetByShopIdAsync(Guid shopId, CancellationToken ct = default)
        => _context.ShopDashboardData.FirstOrDefaultAsync(d => d.ShopId == shopId, ct);
}