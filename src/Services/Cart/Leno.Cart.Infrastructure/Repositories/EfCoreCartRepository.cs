using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Repositories;

/// <summary>
/// 购物车 EF Core 仓储实现，以 UserId 为唯一键管理购物车聚合。
/// </summary>
/// <remarks>
/// P1-8：读路径新增 <c>AsNoTracking</c> 重载（GetByIdReadOnlyAsync/GetByUserIdReadOnlyAsync），
/// 查询/展示/结算预览等只读场景不再由 ChangeTracker 跟踪实体，降低内存与变更检测开销。
/// 写路径仍使用 <see cref="GetByIdAsync"/>/<see cref="GetByUserIdAsync"/> 跟踪版本。
/// P1-2：新增 <see cref="GetByIdsAsync"/> 批量加载方法，替代 N+1 foreach + GetByIdAsync 模式。
/// </remarks>
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
    public async Task<CartAggregate?> GetByIdReadOnlyAsync(Guid id, CancellationToken ct = default)
        => await _context.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task<CartAggregate?> GetByUserIdReadOnlyAsync(Guid userId, CancellationToken ct = default)
        => await _context.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CartAggregate>> GetByIdsAsync(IReadOnlyCollection<Guid> cartIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cartIds);
        if (cartIds.Count == 0)
        {
            return Array.Empty<CartAggregate>();
        }

        return await _context.Carts
            .Include(c => c.Items)
            .Where(c => cartIds.Contains(c.Id))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(CartAggregate aggregate, CancellationToken ct = default)
        => await _context.Carts.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(CartAggregate aggregate, CancellationToken ct = default)
    {
        // P1-2：消费者不再调用 UpdateAsync（依赖 ChangeTracker 自动检测变更）；
        // 保留方法供显式附加场景使用，但内部不再强制 Update（避免已跟踪实体全字段 Modified）。
        // 若实体未跟踪（AsNoTracking 加载场景），调用方应改用 tracked 加载方式（GetByIdsAsync）。
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.Carts.Attach(aggregate);
            _context.Entry(aggregate).State = EntityState.Modified;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(CartAggregate aggregate, CancellationToken ct = default)
    {
        _context.Carts.Remove(aggregate);
        return Task.CompletedTask;
    }
}
