using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using UserCouponAggregate = Leno.Promotion.Domain.Aggregates.UserCoupon;

namespace Leno.Promotion.Infrastructure.Repositories;

/// <summary>
/// 用户优惠券 EF Core 仓储实现。
/// </summary>
public sealed class EfCoreUserCouponRepository : IUserCouponRepository
{
    private readonly PromotionDbContext _context;

    public EfCoreUserCouponRepository(PromotionDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<UserCouponAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.UserCoupons.FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<UserCouponAggregate>> GetByUserAsync(
        Guid userId,
        CouponStatus? status,
        CancellationToken ct = default)
    {
        var query = _context.UserCoupons.Where(u => u.UserId == userId);
        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        return await query.OrderByDescending(u => u.ReceivedAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid userId, Guid couponId, CancellationToken ct = default)
        => await _context.UserCoupons.AnyAsync(u => u.UserId == userId && u.CouponId == couponId, ct);

    /// <inheritdoc />
    public async Task<UserCouponAggregate?> GetByUserIdAndCouponIdAsync(Guid userId, Guid couponId, CancellationToken ct = default)
        => await _context.UserCoupons.FirstOrDefaultAsync(u => u.UserId == userId && u.CouponId == couponId, ct);

    /// <inheritdoc />
    public async Task<UserCouponAggregate?> GetByLockedOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.UserCoupons.FirstOrDefaultAsync(u => u.LockedOrderId == orderId, ct);

    /// <inheritdoc />
    public async Task<List<UserCouponAggregate>> GetAllByLockedOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.UserCoupons
            .Where(u => u.LockedOrderId == orderId)
            .OrderByDescending(u => u.ReceivedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<UserCouponAggregate?> GetByUsedOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.UserCoupons.FirstOrDefaultAsync(u => u.UsedOrderId == orderId, ct);

    /// <inheritdoc />
    public async Task AddAsync(UserCouponAggregate aggregate, CancellationToken ct = default)
        => await _context.UserCoupons.AddAsync(aggregate, ct);

    /// <inheritdoc />
    public Task UpdateAsync(UserCouponAggregate aggregate, CancellationToken ct = default)
    {
        _context.UserCoupons.Update(aggregate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 方法名保持 <c>GetExpiredUnusedCouponsAsync</c> 以避免破坏调用方签名，
    /// 语义扩展为"过期且未核销（Unused 或 Locked）"：
    /// UserCoupon.Expire 允许从 Locked 转 Expired（订单长时间挂起导致 Locked+Expired 券被永久占位），
    /// 扫描必须同时覆盖两态，否则 Locked+Expired 券永远不会被触及。
    /// </remarks>
    public async Task<List<UserCouponAggregate>> GetExpiredUnusedCouponsAsync(
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.UserCoupons
            .Where(u => (u.Status == CouponStatus.Unused || u.Status == CouponStatus.Locked)
                        && u.ExpiredAt.HasValue && u.ExpiredAt.Value < now)
            .OrderBy(u => u.ExpiredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task RemoveAsync(UserCouponAggregate aggregate, CancellationToken ct = default)
    {
        _context.UserCoupons.Remove(aggregate);
        return Task.CompletedTask;
    }
}
