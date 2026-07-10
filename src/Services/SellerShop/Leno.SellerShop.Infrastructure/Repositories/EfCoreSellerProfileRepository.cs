using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SellerShop.Infrastructure.Repositories;

/// <summary>
/// 卖家档案仓储 EF Core 实现。查询带跟踪以支持写场景增量更新。
/// </summary>
public sealed class EfCoreSellerProfileRepository : ISellerProfileRepository
{
    private readonly SellerShopDbContext _context;

    public EfCoreSellerProfileRepository(SellerShopDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<SellerProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.SellerProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public Task<SellerProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _context.SellerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    /// <inheritdoc />
    public Task AddAsync(SellerProfile aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.SellerProfiles.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(SellerProfile aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.SellerProfiles.Attach(aggregate);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(SellerProfile aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.SellerProfiles.Remove(aggregate);
        return Task.CompletedTask;
    }
}
