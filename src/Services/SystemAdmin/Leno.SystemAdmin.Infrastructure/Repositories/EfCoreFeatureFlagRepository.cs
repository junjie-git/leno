using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 特性开关 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreFeatureFlagRepository : IFeatureFlagRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreFeatureFlagRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<FeatureFlag?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.FeatureFlags.FirstOrDefaultAsync(f => f.Id == id, ct);

    /// <inheritdoc />
    public Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default)
        => _context.FeatureFlags.FirstOrDefaultAsync(f => f.Key == key, ct);

    /// <inheritdoc />
    public async Task<List<FeatureFlag>> QueryAsync(string? key, FeatureFlagStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.FeatureFlags.AsQueryable(), key, status);
        return await query
            .OrderByDescending(f => f.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? key, FeatureFlagStatus? status, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.FeatureFlags.AsQueryable(), key, status);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(FeatureFlag aggregate, CancellationToken ct = default)
        => await _context.FeatureFlags.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(FeatureFlag aggregate, CancellationToken ct = default)
    {
        _context.FeatureFlags.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(FeatureFlag aggregate, CancellationToken ct = default)
    {
        _context.FeatureFlags.Remove(aggregate);
        return Task.CompletedTask;
    }

    private static IQueryable<FeatureFlag> ApplyFilters(
        IQueryable<FeatureFlag> query,
        string? key,
        FeatureFlagStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            query = query.Where(f => f.Key.Contains(key));
        }

        if (status.HasValue)
        {
            var isEnabled = status.Value == FeatureFlagStatus.Enabled;
            query = query.Where(f => f.IsEnabled == isEnabled);
        }

        return query;
    }
}
