using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 系统配置 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreSystemConfigRepository : ISystemConfigRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreSystemConfigRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<SystemConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.SystemConfigs.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public Task<SystemConfig?> GetByKeyAsync(string key, CancellationToken ct = default)
        => _context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key, ct);

    /// <inheritdoc />
    public async Task<List<SystemConfig>> QueryByGroupAsync(string? group, CancellationToken ct = default)
    {
        var query = _context.SystemConfigs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(group))
        {
            query = query.Where(c => c.Group == group);
        }

        return await query.OrderByDescending(c => c.UpdatedAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<SystemConfig>> QueryAsync(string? key, string? group, ConfigStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.SystemConfigs.AsQueryable(), key, group, status);
        return await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? key, string? group, ConfigStatus? status, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.SystemConfigs.AsQueryable(), key, group, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<string>> GetDistinctGroupsAsync(CancellationToken ct = default)
    {
        // SQL 层 SELECT DISTINCT Group，避免加载全部配置后内存 Distinct
        return await _context.SystemConfigs
            .Select(c => c.Group)
            .Distinct()
            .OrderBy(g => g)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(SystemConfig aggregate, CancellationToken ct = default)
        => await _context.SystemConfigs.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(SystemConfig aggregate, CancellationToken ct = default)
    {
        _context.SystemConfigs.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(SystemConfig aggregate, CancellationToken ct = default)
    {
        _context.SystemConfigs.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<SystemConfig> ApplyFilters(
        IQueryable<SystemConfig> query,
        string? key,
        string? group,
        ConfigStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            query = query.Where(c => c.Key.Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(group))
        {
            query = query.Where(c => c.Group == group);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        return query;
    }
}
