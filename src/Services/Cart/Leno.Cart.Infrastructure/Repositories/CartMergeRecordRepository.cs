using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Cart.Infrastructure.Repositories;

/// <summary>
/// 匿名购物车合并记录 EF Core 仓储实现。
/// 与 CartDbContext 共享同一 Scoped 生命周期，AddAsync 加入 ChangeTracker 后由 UnitOfWork 统一提交。
/// </summary>
public sealed class CartMergeRecordRepository : ICartMergeRecordRepository
{
    private readonly CartDbContext _context;

    public CartMergeRecordRepository(CartDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string anonymousId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anonymousId);
        return _context.Set<CartMergeRecord>()
            .AsNoTracking()
            .AnyAsync(r => r.AnonymousId == anonymousId, ct);
    }

    /// <inheritdoc />
    public Task AddAsync(CartMergeRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _context.Set<CartMergeRecord>().Add(record);
        return Task.CompletedTask;
    }
}
