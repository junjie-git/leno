using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using DeliveryStatistics = Leno.Notification.Domain.Repositories.DeliveryStatistics;

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
    public async Task<List<NotificationRecord>> GetByIdsAsync(List<Guid> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return new List<NotificationRecord>();
        }

        // 使用 Contains 翻译为 IN 查询，一次性加载所有匹配记录
        return await _context.NotificationRecords
            .Where(n => ids.Contains(n.Id))
            .ToListAsync(ct);
    }

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
            .Where(n => n.Status == NotificationStatus.Failed && n.RetryCount < n.MaxRetry)
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
    public Task<NotificationRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => _context.NotificationRecords.FirstOrDefaultAsync(n => n.IdempotencyKey == idempotencyKey, ct);

    /// <inheritdoc />
    public async Task<List<NotificationRecord>> GetRetriedWithExpiredNextRetryAsync(int limit, CancellationToken ct = default)
    {
        return await _context.NotificationRecords
            .Where(n => n.Status == NotificationStatus.Retried
                        && n.NextRetryAt != null
                        && n.NextRetryAt <= DateTime.UtcNow)
            .OrderBy(n => n.NextRetryAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<NotificationRecord>> GetDeadLetteredAsync(int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.NotificationRecords
            .Where(n => n.Status == NotificationStatus.DeadLettered)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountDeadLetteredAsync(CancellationToken ct = default)
    {
        return await _context.NotificationRecords
            .Where(n => n.Status == NotificationStatus.DeadLettered)
            .CountAsync(ct);
    }

    /// <inheritdoc />
    public Task RemoveAsync(NotificationRecord aggregate, CancellationToken ct = default)
    {
        _context.NotificationRecords.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<NotificationRecord?> GetByChannelMessageIdAsync(string channelMessageId, CancellationToken ct = default)
        => _context.NotificationRecords.FirstOrDefaultAsync(n => n.ChannelMessageId == channelMessageId, ct);

    /// <inheritdoc />
    public async Task<List<NotificationRecord>> QueryRecordsAsync(
        Guid? userId, NotificationChannel? channel, NotificationStatus? status,
        string? templateCode, string? businessRef, DateTime? fromTime, DateTime? toTime,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.NotificationRecords.AsQueryable();

        if (userId.HasValue)
            query = query.Where(n => n.UserId == userId.Value);
        if (channel.HasValue)
            query = query.Where(n => n.Channel == channel.Value);
        if (status.HasValue)
            query = query.Where(n => n.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(templateCode))
            query = query.Where(n => n.TemplateCode == templateCode);
        if (!string.IsNullOrWhiteSpace(businessRef))
            query = query.Where(n => n.BusinessRef == businessRef);
        if (fromTime.HasValue)
            query = query.Where(n => n.CreatedAt >= fromTime.Value);
        if (toTime.HasValue)
            query = query.Where(n => n.CreatedAt <= toTime.Value);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountRecordsAsync(
        Guid? userId, NotificationChannel? channel, NotificationStatus? status,
        string? templateCode, string? businessRef, DateTime? fromTime, DateTime? toTime,
        CancellationToken ct = default)
    {
        var query = _context.NotificationRecords.AsQueryable();

        if (userId.HasValue)
            query = query.Where(n => n.UserId == userId.Value);
        if (channel.HasValue)
            query = query.Where(n => n.Channel == channel.Value);
        if (status.HasValue)
            query = query.Where(n => n.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(templateCode))
            query = query.Where(n => n.TemplateCode == templateCode);
        if (!string.IsNullOrWhiteSpace(businessRef))
            query = query.Where(n => n.BusinessRef == businessRef);
        if (fromTime.HasValue)
            query = query.Where(n => n.CreatedAt >= fromTime.Value);
        if (toTime.HasValue)
            query = query.Where(n => n.CreatedAt <= toTime.Value);

        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<NotificationRecord>> GetByBusinessRefAsync(string businessRef, CancellationToken ct = default)
    {
        return await _context.NotificationRecords
            .Where(n => n.BusinessRef == businessRef)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<DeliveryStatistics>> GetDeliveryStatisticsAsync(DateTime? fromTime, DateTime? toTime, CancellationToken ct = default)
    {
        var query = _context.NotificationRecords.AsQueryable();

        if (fromTime.HasValue)
            query = query.Where(n => n.CreatedAt >= fromTime.Value);
        if (toTime.HasValue)
            query = query.Where(n => n.CreatedAt <= toTime.Value);

        return await query
            .GroupBy(n => new { n.Channel, n.TemplateCode })
            .Select(g => new DeliveryStatistics
            {
                Channel = g.Key.Channel,
                TemplateCode = g.Key.TemplateCode,
                TotalCount = g.Count(),
                SucceededCount = g.Count(n => n.Status == NotificationStatus.Succeeded),
                FailedCount = g.Count(n => n.Status == NotificationStatus.Failed),
                DeadLetteredCount = g.Count(n => n.Status == NotificationStatus.DeadLettered)
            })
            .ToListAsync(ct);
    }
}
