using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Repositories;

/// <summary>
/// 购物车 EF Core 仓储实现，以 UserId 为唯一键管理购物车聚合。
/// </summary>
public sealed class EfCoreCartRepository : ICartRepository
{
    private readonly CartDbContext _context;

    public EfCoreCartRepository(CartDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<CartAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task<CartAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

    /// <inheritdoc />
    public async Task AddAsync(CartAggregate aggregate, CancellationToken ct = default)
        => await _context.Carts.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(CartAggregate aggregate, CancellationToken ct = default)
    {
        _context.Carts.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(CartAggregate aggregate, CancellationToken ct = default)
    {
        _context.Carts.Remove(aggregate);
        return Task.CompletedTask;
    }
}
