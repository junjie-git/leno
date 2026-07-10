using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.Notification.Infrastructure.Repositories;

/// <summary>
/// 通知偏好 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreNotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly NotificationDbContext _context;

    public EfCoreNotificationPreferenceRepository(NotificationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<NotificationPreference?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.NotificationPreferences.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _context.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    /// <inheritdoc />
    public async Task AddAsync(NotificationPreference aggregate, CancellationToken ct = default)
        => await _context.NotificationPreferences.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(NotificationPreference aggregate, CancellationToken ct = default)
    {
        _context.NotificationPreferences.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(NotificationPreference aggregate, CancellationToken ct = default)
    {
        _context.NotificationPreferences.Remove(aggregate);
        return Task.CompletedTask;
    }
}
