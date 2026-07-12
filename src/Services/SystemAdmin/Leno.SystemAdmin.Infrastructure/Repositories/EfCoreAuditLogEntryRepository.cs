using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 跨域审计日志条目 EF Core 仓储实现，仅追加不可变更。
/// </summary>
public sealed class EfCoreAuditLogEntryRepository : IAuditLogEntryRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreAuditLogEntryRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(AuditLogEntry entry, CancellationToken ct = default)
        => await _context.AuditLogEntries.AddAsync(entry, ct);

    /// <inheritdoc />
    public Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.AuditLogEntries.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public Task<AuditLogEntry?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default)
        => _context.AuditLogEntries.FirstOrDefaultAsync(a => a.EventId == eventId, ct);

    /// <inheritdoc />
    public async Task<List<AuditLogEntry>> QueryAsync(string? moduleName, string? action, DateTime? fromTime, DateTime? toTime, Guid? operatorId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.AuditLogEntries.AsQueryable(), moduleName, action, fromTime, toTime, operatorId);
        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? moduleName, string? action, DateTime? fromTime, DateTime? toTime, Guid? operatorId, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.AuditLogEntries.AsQueryable(), moduleName, action, fromTime, toTime, operatorId);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(DateTime before, CancellationToken ct = default)
    {
        return await _context.AuditLogEntries
            .Where(a => a.Timestamp < before)
            .ExecuteDeleteAsync(ct);
    }

    private static IQueryable<AuditLogEntry> ApplyFilters(
        IQueryable<AuditLogEntry> query,
        string? moduleName,
        string? action,
        DateTime? fromTime,
        DateTime? toTime,
        Guid? operatorId)
    {
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            query = query.Where(a => a.Module == moduleName);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (fromTime.HasValue)
        {
            query = query.Where(a => a.Timestamp >= fromTime.Value);
        }

        if (toTime.HasValue)
        {
            query = query.Where(a => a.Timestamp <= toTime.Value);
        }

        if (operatorId.HasValue && operatorId.Value != Guid.Empty)
        {
            query = query.Where(a => a.OperatorId == operatorId.Value);
        }

        return query;
    }
}