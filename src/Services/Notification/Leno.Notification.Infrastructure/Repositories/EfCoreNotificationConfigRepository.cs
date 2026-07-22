using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Notification.Infrastructure.Repositories;

/// <summary>
/// 通知渠道配置 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreNotificationConfigRepository : INotificationConfigRepository
{
    private readonly NotificationDbContext _context;

    public EfCoreNotificationConfigRepository(NotificationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<NotificationConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.NotificationConfigs.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public Task<NotificationConfig?> GetAsync(NotificationChannel channel, string configKey, CancellationToken ct = default)
        => _context.NotificationConfigs.FirstOrDefaultAsync(c => c.Channel == channel && c.ConfigKey == configKey, ct);

    /// <inheritdoc />
    public async Task<List<NotificationConfig>> GetByChannelAsync(NotificationChannel channel, CancellationToken ct = default)
        => await _context.NotificationConfigs.Where(c => c.Channel == channel).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<NotificationConfig>> GetAllAsync(CancellationToken ct = default)
        => await _context.NotificationConfigs.ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(NotificationConfig aggregate, CancellationToken ct = default)
        => await _context.NotificationConfigs.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(NotificationConfig aggregate, CancellationToken ct = default)
    {
        _context.NotificationConfigs.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(NotificationConfig aggregate, CancellationToken ct = default)
    {
        _context.NotificationConfigs.Remove(aggregate);
        return Task.CompletedTask;
    }
}
