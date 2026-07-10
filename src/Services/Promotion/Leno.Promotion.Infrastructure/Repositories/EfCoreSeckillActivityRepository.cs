using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SeckillActivityAggregate = Leno.Promotion.Domain.Aggregates.SeckillActivity;

namespace Leno.Promotion.Infrastructure.Repositories;

/// <summary>
/// 秒杀活动 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreSeckillActivityRepository : ISeckillActivityRepository
{
    private readonly PromotionDbContext _context;

    public EfCoreSeckillActivityRepository(PromotionDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<SeckillActivityAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SeckillActivities.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<SeckillActivityAggregate>> GetActiveAsync(DateTime now, CancellationToken ct = default)
        => await _context.SeckillActivities
            .Where(s => s.Status == SeckillStatus.Active && s.StartTime <= now && s.EndTime > now)
            .OrderBy(s => s.EndTime)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<SeckillActivityAggregate>> GetByStatusAsync(
        SeckillStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.SeckillActivities.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<SeckillActivityAggregate?> GetActiveBySkuIdAsync(Guid skuId, DateTime now, CancellationToken ct = default)
        => await _context.SeckillActivities
            .FirstOrDefaultAsync(s => s.SkuId == skuId
                && s.Status == SeckillStatus.Active
                && s.StartTime <= now
                && s.EndTime > now, ct);

    /// <inheritdoc />
    public async Task AddAsync(SeckillActivityAggregate aggregate, CancellationToken ct = default)
        => await _context.SeckillActivities.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(SeckillActivityAggregate aggregate, CancellationToken ct = default)
    {
        _context.SeckillActivities.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(SeckillActivityAggregate aggregate, CancellationToken ct = default)
    {
        _context.SeckillActivities.Remove(aggregate);
        return Task.CompletedTask;
    }
}
