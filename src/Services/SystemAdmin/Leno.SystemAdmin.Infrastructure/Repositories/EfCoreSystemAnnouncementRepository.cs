using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 系统公告 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreSystemAnnouncementRepository : ISystemAnnouncementRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreSystemAnnouncementRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<SystemAnnouncement?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.SystemAnnouncements.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<SystemAnnouncement>> QueryAsync(AnnouncementType? announcementType, AnnouncementStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.SystemAnnouncements.AsQueryable(), announcementType, status);
        return await query
            .OrderByDescending(a => a.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(AnnouncementType? announcementType, AnnouncementStatus? status, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.SystemAnnouncements.AsQueryable(), announcementType, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<SystemAnnouncement>> GetPublishedAsync(DateTime now, int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.SystemAnnouncements
            .Where(a => a.Status == AnnouncementStatus.Published
                && a.PublishAt <= now
                && (a.ExpireAt == null || a.ExpireAt > now))
            .OrderByDescending(a => a.PublishAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(SystemAnnouncement aggregate, CancellationToken ct = default)
        => await _context.SystemAnnouncements.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(SystemAnnouncement aggregate, CancellationToken ct = default)
    {
        _context.SystemAnnouncements.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(SystemAnnouncement aggregate, CancellationToken ct = default)
    {
        _context.SystemAnnouncements.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<SystemAnnouncement> ApplyFilters(
        IQueryable<SystemAnnouncement> query,
        AnnouncementType? announcementType,
        AnnouncementStatus? status)
    {
        if (announcementType.HasValue)
        {
            query = query.Where(a => a.Type == announcementType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        return query;
    }
}
