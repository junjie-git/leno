using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 定时任务 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreScheduledTaskRepository : IScheduledTaskRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreScheduledTaskRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<ScheduledTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.ScheduledTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<ScheduledTask>> GetEnabledAsync(CancellationToken ct = default)
        => await _context.ScheduledTasks
            .Where(t => t.Status == ScheduledTaskStatus.Enabled)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<ScheduledTask>> QueryAsync(string? name, ScheduledTaskStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.ScheduledTasks.AsQueryable(), name, status);
        return await query
            .OrderByDescending(t => t.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? name, ScheduledTaskStatus? status, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.ScheduledTasks.AsQueryable(), name, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(ScheduledTask aggregate, CancellationToken ct = default)
        => await _context.ScheduledTasks.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(ScheduledTask aggregate, CancellationToken ct = default)
    {
        _context.ScheduledTasks.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(ScheduledTask aggregate, CancellationToken ct = default)
    {
        _context.ScheduledTasks.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<ScheduledTask> ApplyFilters(
        IQueryable<ScheduledTask> query,
        string? name,
        ScheduledTaskStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(t => t.Name.Contains(name));
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return query;
    }
}
