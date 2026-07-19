using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using CouponAggregate = Leno.Promotion.Domain.Aggregates.Coupon;
using UserCouponAggregate = Leno.Promotion.Domain.Aggregates.UserCoupon;

namespace Leno.Promotion.Application.Services;

/// <summary>
/// 优惠券管理应用服务实现。
/// </summary>
public sealed class CouponAppService : ICouponAppService
{
    private readonly ICouponRepository _couponRepository;
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CouponAppService(
        ICouponRepository couponRepository,
        IUserCouponRepository userCouponRepository,
        IUnitOfWork unitOfWork)
    {
        _couponRepository = couponRepository;
        _userCouponRepository = userCouponRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<CouponDto> CreateAsync(CreateCouponDto dto, CancellationToken ct = default)
    {
        var coupon = CouponAggregate.Create(
            Guid.NewGuid(), dto.Name, dto.Type, dto.FaceValue, dto.MinSpend,
            dto.ValidityType, dto.ValidFrom, dto.ValidTo, dto.ValidDays, dto.TotalQty);

        await _couponRepository.AddAsync(coupon, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(coupon);
    }

    /// <inheritdoc />
    public async Task<CouponDto> UpdateAsync(Guid couponId, UpdateCouponDto dto, CancellationToken ct = default)
    {
        var coupon = await RequireCouponAsync(couponId, ct);
        coupon.Update(dto.Name, dto.Type, dto.FaceValue, dto.MinSpend,
            dto.ValidityType, dto.ValidFrom, dto.ValidTo, dto.ValidDays);

        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(coupon);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid couponId, CancellationToken ct = default)
    {
        var coupon = await RequireCouponAsync(couponId, ct);
        coupon.Enable();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid couponId, CancellationToken ct = default)
    {
        var coupon = await RequireCouponAsync(couponId, ct);
        coupon.Disable();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task IssueAsync(Guid couponId, int quantity, CancellationToken ct = default)
    {
        var coupon = await RequireCouponAsync(couponId, ct);
        coupon.Issue(quantity);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<CouponDto>> QueryAsync(CouponTemplateStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var coupons = await _couponRepository.GetByStatusAsync(status, page, pageSize, ct);
        return coupons.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<List<CouponDto>> GetReceivableAsync(CancellationToken ct = default)
    {
        var coupons = await _couponRepository.GetReceivableAsync(DateTime.UtcNow, ct);
        return coupons.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<UserCouponDto> ReceiveAsync(Guid userId, Guid couponId, string source, CancellationToken ct = default)
    {
        var coupon = await RequireCouponAsync(couponId, ct);

        // 重复领取校验
        if (await _userCouponRepository.ExistsAsync(userId, couponId, ct))
        {
            throw new PromotionDomainException("已领取过该优惠券，不可重复领取", "COUPON_ALREADY_RECEIVED");
        }

        // 校验可领取
        if (!coupon.IsReceivable(DateTime.UtcNow))
        {
            throw new PromotionDomainException("优惠券不可领取（已停用/已过期/已发完）", "COUPON_NOT_RECEIVABLE");
        }

        // 发放
        coupon.Issue(1);

        var expiredAt = coupon.ComputeExpiredAt(DateTime.UtcNow);
        var userCoupon = UserCouponAggregate.Receive(Guid.NewGuid(), userId, couponId, source, expiredAt);

        await _userCouponRepository.AddAsync(userCoupon, ct);
        try
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // 并发领取：另一请求已先插入同一 (UserId, CouponId) 唯一索引记录，由数据库拒绝第二条插入
            throw new PromotionDomainException("已领取过该优惠券，不可重复领取", "COUPON_ALREADY_RECEIVED");
        }

        return ToDto(userCoupon);
    }

    /// <inheritdoc />
    public async Task<List<UserCouponDto>> GetMyCouponsAsync(Guid userId, CouponStatus? status, CancellationToken ct = default)
    {
        var userCoupons = await _userCouponRepository.GetByUserAsync(userId, status, ct);
        return userCoupons.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task LockCouponAsync(Guid userId, Guid couponId, Guid orderId, CancellationToken ct = default)
    {
        var userCoupon = await _userCouponRepository.GetByUserIdAndCouponIdAsync(userId, couponId, ct)
            ?? throw new PromotionDomainException($"用户 {userId} 未持有优惠券 {couponId}，无法锁定", "USER_COUPON_NOT_FOUND");

        // Lock 内部校验 Unused + 未过期，券已被并发订单占用时抛 USER_COUPON_LOCK_INVALID，由此实现并发互斥
        userCoupon.Lock(orderId);

        await _userCouponRepository.UpdateAsync(userCoupon, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default)
    {
        var lockedCoupons = await _userCouponRepository.GetAllByLockedOrderIdAsync(orderId, ct)
            .ConfigureAwait(false);
        if (lockedCoupons is null || lockedCoupons.Count == 0)
        {
            return; // 无锁定券，幂等返回
        }
        foreach (var coupon in lockedCoupons)
        {
            // Release 领域方法：Locked → Unused（已过期则 → Expired），状态机校验由聚合根负责
            coupon.Release();
        }
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
    }

    private async Task<CouponAggregate> RequireCouponAsync(Guid couponId, CancellationToken ct)
        => await _couponRepository.GetByIdAsync(couponId, ct)
           ?? throw new PromotionDomainException($"优惠券 {couponId} 不存在", "COUPON_NOT_FOUND");

    private static CouponDto ToDto(CouponAggregate coupon)
        => new()
        {
            Id = coupon.Id,
            Name = coupon.Name,
            Type = coupon.Type,
            FaceValue = coupon.FaceValue,
            MinSpend = coupon.MinSpend,
            ValidityType = coupon.ValidityType,
            ValidFrom = coupon.ValidFrom,
            ValidTo = coupon.ValidTo,
            ValidDays = coupon.ValidDays,
            TotalQty = coupon.TotalQty,
            IssuedQty = coupon.IssuedQty,
            Status = coupon.Status,
            CreatedAt = coupon.CreatedAt
        };

    private static UserCouponDto ToDto(UserCouponAggregate userCoupon)
        => new()
        {
            Id = userCoupon.Id,
            UserId = userCoupon.UserId,
            CouponId = userCoupon.CouponId,
            Status = userCoupon.Status,
            Source = userCoupon.Source,
            ReceivedAt = userCoupon.ReceivedAt,
            UsedAt = userCoupon.UsedAt,
            ExpiredAt = userCoupon.ExpiredAt
        };
}
