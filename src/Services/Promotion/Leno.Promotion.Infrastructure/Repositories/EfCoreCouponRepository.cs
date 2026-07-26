using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using CouponAggregate = Leno.Promotion.Domain.Aggregates.Coupon;

namespace Leno.Promotion.Infrastructure.Repositories;

/// <summary>
/// 优惠券模板 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreCouponRepository : ICouponRepository
{
    private readonly PromotionDbContext _context;

    public EfCoreCouponRepository(PromotionDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<CouponAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<CouponAggregate>> GetReceivableAsync(DateTime now, CancellationToken ct = default)
    {
        var coupons = await _context.Coupons
            .Where(c => c.Status == CouponTemplateStatus.Enabled)
            .ToListAsync(ct);

        return coupons.Where(c => c.IsReceivable(now)).ToList();
    }

    /// <inheritdoc />
    public async Task<List<CouponAggregate>> QueryAsync(
        string? name,
        CouponType? type,
        CouponTemplateStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = BuildQuery(name, type, status);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        string? name,
        CouponType? type,
        CouponTemplateStatus? status,
        CancellationToken ct = default)
    {
        var query = BuildQuery(name, type, status);
        return await query.CountAsync(ct);
    }

    /// <summary>
    /// 构建带筛选条件的 IQueryable，供 QueryAsync 与 CountAsync 复用，确保两处筛选逻辑一致。
    /// name 非空白时按 Name Contains 模糊匹配；type 非空时按 Type 精确匹配；
    /// status 非空时按 Status 精确匹配。
    /// </summary>
    private IQueryable<CouponAggregate> BuildQuery(
        string? name,
        CouponType? type,
        CouponTemplateStatus? status)
    {
        var query = _context.Coupons.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(c => c.Name.Contains(name));
        }

        if (type.HasValue)
        {
            query = query.Where(c => c.Type == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        return query;
    }

    /// <inheritdoc />
    public async Task<List<CouponAggregate>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new List<CouponAggregate>();
        }

        // AsNoTracking：试算为只读场景，避免 DbContext Identity Map 缓存膨胀
        return await _context.Coupons
            .AsNoTracking()
            .Where(c => idList.Contains(c.Id))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(CouponAggregate aggregate, CancellationToken ct = default)
        => await _context.Coupons.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(CouponAggregate aggregate, CancellationToken ct = default)
    {
        _context.Coupons.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(CouponAggregate aggregate, CancellationToken ct = default)
    {
        _context.Coupons.Remove(aggregate);
        return Task.CompletedTask;
    }
}
