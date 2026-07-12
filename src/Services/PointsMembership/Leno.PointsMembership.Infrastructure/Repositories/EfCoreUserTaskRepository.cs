using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.PointsMembership.Infrastructure.Repositories;

/// <summary>
/// 用户任务 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreUserTaskRepository : IUserTaskRepository
{
    private readonly PointsMembershipDbContext _context;

    public EfCoreUserTaskRepository(PointsMembershipDbContext context)
    {
        _context = context;
    }

    public async Task<UserTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.UserTasks.FindAsync([id], ct);

    public async Task<UserTask?> GetByUserIdAndTaskIdAsync(Guid userId, Guid taskId, CancellationToken ct = default)
        => await _context.UserTasks
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TaskId == taskId, ct);

    public async Task<List<UserTask>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.UserTasks
            .Where(ut => ut.UserId == userId)
            .ToListAsync(ct);

    public async Task<List<UserTask>> GetCompletedByUserIdAndDateAsync(Guid userId, DateOnly targetDate, CancellationToken ct = default)
        => await _context.UserTasks
            .Where(ut => ut.UserId == userId
                && ut.Status == UserTaskStatus.Completed
                && ut.CompletedDate == targetDate)
            .ToListAsync(ct);

    public async Task AddAsync(UserTask entity, CancellationToken ct = default)
        => await _context.UserTasks.AddAsync(entity, ct);

    public Task UpdateAsync(UserTask entity, CancellationToken ct = default)
    {
        _context.UserTasks.Update(entity);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(UserTask entity, CancellationToken ct = default)
    {
        _context.UserTasks.Remove(entity);
        return Task.CompletedTask;
    }
}