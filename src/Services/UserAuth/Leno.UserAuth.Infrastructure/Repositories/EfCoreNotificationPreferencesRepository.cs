using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure.Repositories;

/// <summary>
/// 通知偏好仓储 EF Core 实现。
/// 同一用户仅一条 <see cref="NotificationPreferences"/> 聚合，按 <see cref="NotificationPreferences.UserId"/> 唯一索引查询。
/// </summary>
public sealed class EfCoreNotificationPreferencesRepository : INotificationPreferencesRepository
{
    private readonly UserAuthDbContext _context;

    public EfCoreNotificationPreferencesRepository(UserAuthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<NotificationPreferences?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.NotificationPreferences.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public Task<NotificationPreferences?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _context.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    /// <inheritdoc />
    public Task AddAsync(NotificationPreferences aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.NotificationPreferences.Add(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(NotificationPreferences aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_context.Entry(aggregate).State == EntityState.Detached)
        {
            _context.NotificationPreferences.Attach(aggregate);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(NotificationPreferences aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.NotificationPreferences.Remove(aggregate);
        return Task.CompletedTask;
    }
}
