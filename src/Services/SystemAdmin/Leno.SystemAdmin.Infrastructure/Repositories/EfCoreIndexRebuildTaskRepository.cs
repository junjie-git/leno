using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 索引重建任务 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreIndexRebuildTaskRepository : IIndexRebuildTaskRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreIndexRebuildTaskRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<IndexRebuildTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.IndexRebuildTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <inheritdoc />
    public async Task<IndexRebuildTask?> GetRunningByIndexAsync(string targetContext, string indexName, CancellationToken ct)
        => await _context.IndexRebuildTasks
            .Where(t => t.TargetContext == targetContext
                        && t.IndexName == indexName
                        && (t.Status == RebuildTaskStatus.Created || t.Status == RebuildTaskStatus.Running))
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<List<IndexRebuildTask>> QueryAsync(string? targetContext, RebuildTaskStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = ApplyFilters(_context.IndexRebuildTasks.AsQueryable(), targetContext, status);
        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? targetContext, RebuildTaskStatus? status, CancellationToken ct)
    {
        var query = ApplyFilters(_context.IndexRebuildTasks.AsQueryable(), targetContext, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(IndexRebuildTask aggregate, CancellationToken ct = default)
        => await _context.IndexRebuildTasks.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(IndexRebuildTask aggregate, CancellationToken ct = default)
    {
        _context.IndexRebuildTasks.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(IndexRebuildTask aggregate, CancellationToken ct = default)
    {
        _context.IndexRebuildTasks.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<IndexRebuildTask> ApplyFilters(
        IQueryable<IndexRebuildTask> query,
        string? targetContext,
        RebuildTaskStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(targetContext))
        {
            query = query.Where(t => t.TargetContext.Contains(targetContext));
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return query;
    }
}