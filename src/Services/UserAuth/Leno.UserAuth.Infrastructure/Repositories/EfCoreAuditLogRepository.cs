using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure.Repositories;

/// <summary>
/// 审计日志仓储 EF Core 实现。审计日志仅追加写入，无更新与删除方法。
/// 查询使用 <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>，因审计日志不可变。
/// </summary>
public sealed class EfCoreAuditLogRepository : IAuditLogRepository
{
    private readonly UserAuthDbContext _context;

    public EfCoreAuditLogRepository(UserAuthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task AddAsync(AuditLog auditLog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        _context.AuditLogs.Add(auditLog);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<AuditLog> Items, int Total)> QueryAsync(
        Guid? operatorId = null,
        string? action = null,
        string? resourceType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (operatorId.HasValue)
        {
            query = query.Where(a => a.OperatorId == operatorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            query = query.Where(a => a.ResourceType == resourceType);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.OperatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.OperatedAt <= toDate.Value);
        }

        var total = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var items = await query
            .OrderByDescending(a => a.OperatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
}
