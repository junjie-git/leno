using Leno.UserCenter.Domain.Aggregates;
using Leno.UserCenter.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserCenter.Infrastructure.Repositories;

/// <summary>
/// 浏览历史仓储 EF Core 实现。
/// 列表查询按 <see cref="BrowseHistory.ViewedAt"/> 倒序返回；批量与清空操作均限定 userId 隔离。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class EfCoreBrowseHistoryRepository : IBrowseHistoryRepository
{
    private readonly UserCenterDbContext _context;

    public EfCoreBrowseHistoryRepository(UserCenterDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<BrowseHistory?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.BrowseHistories.FirstOrDefaultAsync(h => h.Id == id, ct);

    /// <inheritdoc />
    public Task<BrowseHistory?> FindLatestByUserAndSpuAsync(Guid userId, Guid spuId, CancellationToken ct = default)
        => _context.BrowseHistories
            .Where(h => h.UserId == userId && h.SpuId == spuId)
            .OrderByDescending(h => h.ViewedAt)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<BrowseHistory> Items, int Total)> QueryAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.BrowseHistories.AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ViewedAt);

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
    public async Task<int> BatchDeleteAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return 0;
        }

        var toDelete = await _context.BrowseHistories
            .Where(h => h.UserId == userId && ids.Contains(h.Id))
            .ToListAsync(ct);

        if (toDelete.Count == 0)
        {
            return 0;
        }

        _context.BrowseHistories.RemoveRange(toDelete);
        return toDelete.Count;
    }

    /// <inheritdoc />
    public async Task<int> ClearAllByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var toDelete = await _context.BrowseHistories
            .Where(h => h.UserId == userId)
            .ToListAsync(ct);

        if (toDelete.Count == 0)
        {
            return 0;
        }

        _context.BrowseHistories.RemoveRange(toDelete);
        return toDelete.Count;
    }

    /// <inheritdoc />
    public Task AddAsync(BrowseHistory aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.BrowseHistories.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(BrowseHistory aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.BrowseHistories.Attach(aggregate);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(BrowseHistory aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.BrowseHistories.Remove(aggregate);
        return Task.CompletedTask;
    }
}
