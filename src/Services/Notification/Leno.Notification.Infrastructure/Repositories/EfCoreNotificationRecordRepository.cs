using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Notification.Infrastructure.Repositories;

/// <summary>
/// 通知记录 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreNotificationRecordRepository : INotificationRecordRepository
{
    private readonly NotificationDbContext _context;

    public EfCoreNotificationRecordRepository(NotificationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<NotificationRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.NotificationRecords.FirstOrDefaultAsync(n => n.Id == id, ct);

    /// <inheritdoc />
    public Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken ct = default)
        => _context.NotificationRecords.AnyAsync(n => n.EventId == eventId, ct);

    /// <inheritdoc />
    public async Task<List<NotificationRecord>> QueryByUserAsync(Guid userId, bool? isRead, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.NotificationRecords
            .Where(n => n.UserId == userId && n.Channel == NotificationChannel.InApp);

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountByUserAsync(Guid userId, bool? isRead, CancellationToken ct = default)
    {
        var query = _context.NotificationRecords
            .Where(n => n.UserId == userId && n.Channel == NotificationChannel.InApp);

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<NotificationRecord>> GetPendingAsync(int limit, CancellationToken ct = default)
    {
        return await _context.NotificationRecords
            .Where(n => n.Status == NotificationStatus.Pending)
            .OrderBy(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<NotificationRecord>> GetRetryableAsync(int limit, CancellationToken ct = default)
    {
        return await _context.NotificationRecords
            .Where(n => n.Status == NotificationStatus.Failed && n.RetryCount < NotificationRecord.MaxRetryCount)
            .OrderBy(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.NotificationRecords
            .Where(n => n.UserId == userId && n.Channel == NotificationChannel.InApp && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(NotificationRecord aggregate, CancellationToken ct = default)
        => await _context.NotificationRecords.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(NotificationRecord aggregate, CancellationToken ct = default)
    {
        _context.NotificationRecords.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(NotificationRecord aggregate, CancellationToken ct = default)
    {
        _context.NotificationRecords.Remove(aggregate);
        return Task.CompletedTask;
    }
}
