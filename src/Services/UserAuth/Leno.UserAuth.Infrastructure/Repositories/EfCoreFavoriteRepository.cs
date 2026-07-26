using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure.Repositories;

/// <summary>
/// 商品收藏仓储 EF Core 实现。
/// 排序字段：comprehensive/created 按 favorited_at，price/sales 由商品域快照字段提供，本域仅持久化关系数据，
/// 故 price/sales 排序返回前端时由 BFF 层组合查询商品快照后再次排序，仓储层退化为 favorited_at 倒序。
/// 用户隔离：所有查询方法均以 <see cref="Favorite.UserId"/> 为过滤条件，杜绝跨用户访问。
/// </summary>
public sealed class EfCoreFavoriteRepository : IFavoriteRepository
{
    private readonly UserAuthDbContext _context;

    public EfCoreFavoriteRepository(UserAuthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<Favorite?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Favorites.FirstOrDefaultAsync(f => f.Id == id, ct);

    /// <inheritdoc />
    public Task<Favorite?> GetByUserAndSpuAsync(Guid userId, Guid spuId, CancellationToken ct = default)
        => _context.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.SpuId == spuId, ct);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Favorite> Items, int Total)> QueryAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        string sort = "created",
        string order = "desc",
        CancellationToken ct = default)
    {
        var query = _context.Favorites.AsNoTracking().Where(f => f.UserId == userId);

        // 仓储层仅支持按 favorited_at 排序；price/sales 排序由 BFF 层组合商品快照后处理
        var isAsc = string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase);
        query = isAsc
            ? query.OrderBy(f => f.FavoritedAt)
            : query.OrderByDescending(f => f.FavoritedAt);

        var total = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default)
        => _context.Favorites.AsNoTracking().CountAsync(f => f.UserId == userId, ct);

    /// <inheritdoc />
    public async Task<int> BatchDeleteAsync(Guid userId, IReadOnlyCollection<Guid> spuIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spuIds);
        if (spuIds.Count == 0)
        {
            return 0;
        }

        var toDelete = await _context.Favorites
            .Where(f => f.UserId == userId && spuIds.Contains(f.SpuId))
            .ToListAsync(ct);

        if (toDelete.Count == 0)
        {
            return 0;
        }

        _context.Favorites.RemoveRange(toDelete);
        return toDelete.Count;
    }

    /// <inheritdoc />
    public Task AddAsync(Favorite aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Favorites.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(Favorite aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.Favorites.Attach(aggregate);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Favorite aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.Favorites.Remove(aggregate);
        return Task.CompletedTask;
    }
}
