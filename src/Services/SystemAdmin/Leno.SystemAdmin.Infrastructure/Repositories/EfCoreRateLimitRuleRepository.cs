using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Repositories;

/// <summary>
/// 限流规则 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreRateLimitRuleRepository : IRateLimitRuleRepository
{
    private readonly SystemAdminDbContext _context;

    public EfCoreRateLimitRuleRepository(SystemAdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<RateLimitRule?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RateLimitRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public async Task AddAsync(RateLimitRule aggregate, CancellationToken ct = default)
        => await _context.RateLimitRules.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(RateLimitRule aggregate, CancellationToken ct = default)
    {
        _context.RateLimitRules.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(RateLimitRule aggregate, CancellationToken ct = default)
    {
        _context.RateLimitRules.Remove(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<List<RateLimitRule>> GetAllEnabledAsync(CancellationToken ct = default)
        => await _context.RateLimitRules
            .Where(r => r.Enabled)
            .OrderBy(r => r.TargetApi)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<RateLimitRule>> QueryAsync(string? targetApi, bool? enabled, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.RateLimitRules.AsQueryable(), targetApi, enabled);
        return await query
            .OrderBy(r => r.TargetApi)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? targetApi, bool? enabled, CancellationToken ct = default)
    {
        var query = ApplyFilters(_context.RateLimitRules.AsQueryable(), targetApi, enabled);
        return await query.CountAsync(ct);
    }

    private static IQueryable<RateLimitRule> ApplyFilters(
        IQueryable<RateLimitRule> query,
        string? targetApi,
        bool? enabled)
    {
        if (!string.IsNullOrWhiteSpace(targetApi))
        {
            query = query.Where(r => r.TargetApi.Contains(targetApi));
        }

        if (enabled.HasValue)
        {
            query = query.Where(r => r.Enabled == enabled.Value);
        }

        return query;
    }
}