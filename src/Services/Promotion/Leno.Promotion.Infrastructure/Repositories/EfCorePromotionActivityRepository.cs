using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using PromotionActivityAggregate = Leno.Promotion.Domain.Aggregates.PromotionActivity;

namespace Leno.Promotion.Infrastructure.Repositories;

/// <summary>
/// 满减/促销活动 EF Core 仓储实现。
/// </summary>
public sealed class EfCorePromotionActivityRepository : IPromotionActivityRepository
{
    private readonly PromotionDbContext _context;

    public EfCorePromotionActivityRepository(PromotionDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PromotionActivityAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PromotionActivities.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<PromotionActivityAggregate>> GetActiveAsync(DateTime now, CancellationToken ct = default)
        => await _context.PromotionActivities
            .Where(a => a.Status == PromotionStatus.Active && a.StartTime <= now && a.EndTime > now)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<PromotionActivityAggregate>> GetByStatusAsync(
        PromotionStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.PromotionActivities.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(PromotionActivityAggregate aggregate, CancellationToken ct = default)
        => await _context.PromotionActivities.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(PromotionActivityAggregate aggregate, CancellationToken ct = default)
    {
        _context.PromotionActivities.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(PromotionActivityAggregate aggregate, CancellationToken ct = default)
    {
        _context.PromotionActivities.Remove(aggregate);
        return Task.CompletedTask;
    }
}
