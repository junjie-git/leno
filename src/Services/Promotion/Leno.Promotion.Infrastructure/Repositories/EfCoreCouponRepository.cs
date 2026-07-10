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
    public async Task<List<CouponAggregate>> GetByStatusAsync(
        CouponTemplateStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Coupons.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
