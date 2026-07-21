using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 审计日志 EF Core 仓储实现，仅追加不可变更。
/// </summary>
public sealed class EfCoreAuditLogRepository : IAuditLogRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreAuditLogRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
        => await _context.AuditLogs.AddAsync(log, ct);

    /// <inheritdoc />
    public Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.AuditLogs.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<AuditLog>> QueryAsync(Guid? operatorId, string? resourceType, DateTime? fromTime, DateTime? toTime, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.AuditLogs.AsQueryable(), operatorId, resourceType, fromTime, toTime);
        return await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid? operatorId, string? resourceType, DateTime? fromTime, DateTime? toTime, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.AuditLogs.AsQueryable(), operatorId, resourceType, fromTime, toTime);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AuditLog> StreamAsync(
        Guid? operatorId,
        string? resourceType,
        DateTime? fromTime,
        DateTime? toTime,
        int maxCount = 100_000,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (maxCount <= 0)
        {
            yield break;
        }

        var query = ApplyFilters(_context.AuditLogs.AsNoTracking(), operatorId, resourceType, fromTime, toTime)
            .OrderByDescending(a => a.OccurredAt)
            .Take(maxCount);

        await foreach (var log in query.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return log;
        }
    }

    private static IQueryable<AuditLog> ApplyFilters(
        IQueryable<AuditLog> query,
        Guid? operatorId,
        string? resourceType,
        DateTime? fromTime,
        DateTime? toTime)
    {
        if (operatorId.HasValue && operatorId.Value != Guid.Empty)
        {
            query = query.Where(a => a.OperatorId == operatorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            query = query.Where(a => a.ResourceType == resourceType);
        }

        if (fromTime.HasValue)
        {
            query = query.Where(a => a.OccurredAt >= fromTime.Value);
        }

        if (toTime.HasValue)
        {
            query = query.Where(a => a.OccurredAt <= toTime.Value);
        }

        return query;
    }
}
