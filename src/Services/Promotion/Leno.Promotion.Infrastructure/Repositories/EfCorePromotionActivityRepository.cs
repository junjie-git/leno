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
    public async Task<List<PromotionActivityAggregate>> QueryAsync(
        string? name,
        PromotionStatus? status,
        DateTime? startTime,
        DateTime? endTime,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = BuildQuery(name, status, startTime, endTime);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        string? name,
        PromotionStatus? status,
        DateTime? startTime,
        DateTime? endTime,
        CancellationToken ct = default)
    {
        var query = BuildQuery(name, status, startTime, endTime);
        return await query.CountAsync(ct);
    }

    /// <summary>
    /// 构建带筛选条件的 IQueryable，供 QueryAsync 与 CountAsync 复用，确保两处筛选逻辑一致。
    /// name 非空白时按 Name Contains 模糊匹配；status 非空时精确匹配；
    /// startTime 非空时按 StartTime &gt;=；endTime 非空时按 EndTime &lt;=。
    /// </summary>
    private IQueryable<PromotionActivityAggregate> BuildQuery(
        string? name,
        PromotionStatus? status,
        DateTime? startTime,
        DateTime? endTime)
    {
        var query = _context.PromotionActivities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(a => a.Name.Contains(name));
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (startTime.HasValue)
        {
            query = query.Where(a => a.StartTime >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            query = query.Where(a => a.EndTime <= endTime.Value);
        }

        return query;
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
