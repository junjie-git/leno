using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 登录日志聚合根 EF Core 仓储实现。
/// 仅追加：无 Update/Delete 方法；StreamAsync 用 AsAsyncEnumerable 流式导出 CSV。
/// </summary>
public sealed class EfCoreLoginLogRepository : ILoginLogRepository
{
    private readonly SystemAdminDbContext _db;

    public EfCoreLoginLogRepository(SystemAdminDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public Task<LoginLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.LoginLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);

    /// <inheritdoc />
    public async Task<(List<LoginLog> Items, int Total)> QueryAsync(LoginLogQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var queryable = ApplyFilters(_db.LoginLogs.AsNoTracking(), query);
        var total = await queryable.CountAsync(ct);
        var items = await queryable
            .OrderByDescending(l => l.LoginAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <inheritdoc />
    public async Task AddAsync(LoginLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await _db.LoginLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LoginLog> StreamAsync(
        LoginLogQuery query,
        int limit,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (limit <= 0)
        {
            yield break;
        }

        var queryable = ApplyFilters(_db.LoginLogs.AsNoTracking(), query)
            .OrderByDescending(l => l.LoginAt)
            .Take(limit);

        await foreach (var log in queryable.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return log;
        }
    }

    /// <inheritdoc />
    public Task<LoginLog?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default)
        => _db.LoginLogs.AsNoTracking().FirstOrDefaultAsync(l => l.EventId == eventId, ct);

    private static IQueryable<LoginLog> ApplyFilters(IQueryable<LoginLog> queryable, LoginLogQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            queryable = queryable.Where(l => l.Username.Contains(query.Username));
        }

        if (query.Result.HasValue)
        {
            queryable = queryable.Where(l => l.Result == query.Result.Value);
        }

        if (query.LoginAtFrom.HasValue)
        {
            queryable = queryable.Where(l => l.LoginAt >= query.LoginAtFrom.Value);
        }

        if (query.LoginAtTo.HasValue)
        {
            queryable = queryable.Where(l => l.LoginAt <= query.LoginAtTo.Value);
        }

        return queryable;
    }
}
