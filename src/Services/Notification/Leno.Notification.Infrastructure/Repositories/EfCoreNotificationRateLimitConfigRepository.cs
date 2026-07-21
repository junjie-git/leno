using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Notification.Infrastructure.Repositories;

/// <summary>
/// 通知限流配置 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreNotificationRateLimitConfigRepository : INotificationRateLimitConfigRepository
{
    private readonly NotificationDbContext _context;

    public EfCoreNotificationRateLimitConfigRepository(NotificationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<NotificationRateLimitConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.NotificationRateLimitConfigs.FirstOrDefaultAsync(n => n.Id == id, ct);

    /// <inheritdoc />
    public Task<NotificationRateLimitConfig?> GetByChannelAsync(NotificationChannel channel, CancellationToken ct = default)
        => _context.NotificationRateLimitConfigs.FirstOrDefaultAsync(n => n.Channel == channel, ct);

    /// <inheritdoc />
    public async Task<List<NotificationRateLimitConfig>> GetAllAsync(CancellationToken ct = default)
        => await _context.NotificationRateLimitConfigs.ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(NotificationRateLimitConfig aggregate, CancellationToken ct = default)
        => await _context.NotificationRateLimitConfigs.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(NotificationRateLimitConfig aggregate, CancellationToken ct = default)
    {
        _context.NotificationRateLimitConfigs.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(NotificationRateLimitConfig aggregate, CancellationToken ct = default)
    {
        _context.NotificationRateLimitConfigs.Remove(aggregate);
        return Task.CompletedTask;
    }
}
