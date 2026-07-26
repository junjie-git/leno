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
    public async Task<List<SeckillActivityAggregate>> QueryAsync(
        string? name,
        SeckillStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = BuildQuery(name, status);

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        string? name,
        SeckillStatus? status,
        CancellationToken ct = default)
    {
        var query = BuildQuery(name, status);
        return await query.CountAsync(ct);
    }

    /// <summary>
    /// 构建带筛选条件的 IQueryable，供 QueryAsync 与 CountAsync 复用，确保两处筛选逻辑一致。
    /// name 非空白时按 Name Contains 模糊匹配；status 非空时按 Status 精确匹配。
    /// </summary>
    private IQueryable<SeckillActivityAggregate> BuildQuery(
        string? name,
        SeckillStatus? status)
    {
        var query = _context.SeckillActivities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(s => s.Name.Contains(name));
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        return query;
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
