using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 任务定义 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreTaskRepository : ITaskRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCoreTaskRepository(PointsMembershipDbContext context)
    {
        _context = context;
    }

    public async Task<TaskDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Tasks.FindAsync([id], ct);

    public async Task<TaskDefinition?> GetByTypeAsync(TaskType type, CancellationToken ct = default)
        => await _context.Tasks.FirstOrDefaultAsync(t => t.Type == type, ct);

    public async Task<List<TaskDefinition>> GetAllEnabledAsync(CancellationToken ct = default)
        => await _context.Tasks.Where(t => t.IsEnabled).ToListAsync(ct);

    public async Task AddAsync(TaskDefinition entity, CancellationToken ct = default)
        => await _context.Tasks.AddAsync(entity, ct);

    public Task UpdateAsync(TaskDefinition entity, CancellationToken ct = default)
    {
        _context.Tasks.Update(entity);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(TaskDefinition entity, CancellationToken ct = default)
    {
        _context.Tasks.Remove(entity);
        return Task.CompletedTask;
    }
}