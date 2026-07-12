using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Notification.Infrastructure.Repositories;

/// <summary>
/// 通知模板 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreNotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly NotificationDbContext _context;

    public EfCoreNotificationTemplateRepository(NotificationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <inheritdoc />
    public Task<NotificationTemplate?> GetEnabledAsync(string code, NotificationChannel channel, CancellationToken ct = default)
        => _context.NotificationTemplates.FirstOrDefaultAsync(
            t => t.Code == code && t.Channel == channel && t.Status == TemplateStatus.Enabled, ct);

    /// <inheritdoc />
    public async Task<List<NotificationTemplate>> QueryAsync(string? code, NotificationChannel? channel, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.NotificationTemplates.AsQueryable(), code, channel);
        return await query
            .OrderByDescending(t => t.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? code, NotificationChannel? channel, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.NotificationTemplates.AsQueryable(), code, channel);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(NotificationTemplate aggregate, CancellationToken ct = default)
        => await _context.NotificationTemplates.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(NotificationTemplate aggregate, CancellationToken ct = default)
    {
        _context.NotificationTemplates.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<NotificationTemplate?> GetEnabledByCodeAsync(string code, CancellationToken ct = default)
        => _context.NotificationTemplates.FirstOrDefaultAsync(
            t => t.Code == code && t.Status == TemplateStatus.Enabled, ct);

    /// <inheritdoc />
    public Task RemoveAsync(NotificationTemplate aggregate, CancellationToken ct = default)
    {
        _context.NotificationTemplates.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<NotificationTemplate> ApplyFilters(
        IQueryable<NotificationTemplate> query,
        string? code,
        NotificationChannel? channel)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            query = query.Where(t => t.Code == code);
        }

        if (channel.HasValue)
        {
            query = query.Where(t => t.Channel == channel.Value);
        }

        return query;
    }
}