using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 操作日志 EF Core 仓储实现，仅追加不可变更。
/// </summary>
public sealed class EfCoreOperationLogRepository : IOperationLogRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreOperationLogRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(OperationLog log, CancellationToken ct = default)
        => await _context.OperationLogs.AddAsync(log, ct);

    /// <inheritdoc />
    public Task<OperationLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.OperationLogs.FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <inheritdoc />
    public Task<OperationLog?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default)
        => _context.OperationLogs.FirstOrDefaultAsync(l => l.EventId == eventId, ct);

    /// <inheritdoc />
    public async Task<List<OperationLog>> QueryAsync(Guid? operatorId, string? moduleName, DateTime? fromTime, DateTime? toTime, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.OperationLogs.AsQueryable(), operatorId, moduleName, fromTime, toTime);
        return await query
            .OrderByDescending(o => o.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid? operatorId, string? moduleName, DateTime? fromTime, DateTime? toTime, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.OperationLogs.AsQueryable(), operatorId, moduleName, fromTime, toTime);
        return await query.CountAsync(ct);
    }

    private static IQueryable<OperationLog> ApplyFilters(
        IQueryable<OperationLog> query,
        Guid? operatorId,
        string? moduleName,
        DateTime? fromTime,
        DateTime? toTime)
    {
        if (operatorId.HasValue && operatorId.Value != Guid.Empty)
        {
            query = query.Where(o => o.OperatorId == operatorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            query = query.Where(o => o.Module == moduleName);
        }

        if (fromTime.HasValue)
        {
            query = query.Where(o => o.OccurredAt >= fromTime.Value);
        }

        if (toTime.HasValue)
        {
            query = query.Where(o => o.OccurredAt <= toTime.Value);
        }

        return query;
    }
}
