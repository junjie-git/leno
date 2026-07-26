using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// Outbox 归档历史 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreOutboxArchiveRecordRepository : IOutboxArchiveRecordRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreOutboxArchiveRecordRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<OutboxArchiveRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.OutboxArchiveRecords.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<OutboxArchiveRecord>> QueryAsync(string? context, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilter(_context.OutboxArchiveRecords.AsQueryable(), context);
        return await query
            .OrderByDescending(r => r.ArchivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? context, CancellationToken ct = default)
    {
        var query = ApplyFilter(_context.OutboxArchiveRecords.AsQueryable(), context);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLastArchivedAtAsync(string context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return null;
        }
        var last = await _context.OutboxArchiveRecords
            .Where(r => r.Context == context)
            .OrderByDescending(r => r.ArchivedAt)
            .FirstOrDefaultAsync(ct);
        return last?.ArchivedAt;
    }

    /// <inheritdoc />
    public async Task AddAsync(OutboxArchiveRecord aggregate, CancellationToken ct = default)
        => await _context.OutboxArchiveRecords.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(OutboxArchiveRecord aggregate, CancellationToken ct = default)
    {
        _context.OutboxArchiveRecords.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(OutboxArchiveRecord aggregate, CancellationToken ct = default)
    {
        _context.OutboxArchiveRecords.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<OutboxArchiveRecord> ApplyFilter(
        IQueryable<OutboxArchiveRecord> query,
        string? context)
    {
        if (!string.IsNullOrWhiteSpace(context))
        {
            query = query.Where(r => r.Context == context.Trim());
        }
        return query;
    }
}
