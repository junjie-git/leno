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
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // 并发领取：另一请求已先插入同一 (UserId, CouponId) 唯一索引记录，由数据库拒绝第二条插入
            // 仅唯一索引/约束冲突转业务异常；其他 DbUpdateException（连接失败、其他约束冲突等）原样上抛
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
        try
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // 乐观锁冲突：另一并发请求已先行修改该券（如已被其他订单 Lock），rowversion 不匹配
            // 转业务异常避免 500，调用方可重试或回退
            throw new PromotionDomainException(
                "券已被并发订单锁定，请重试", "USER_COUPON_LOCK_INVALID");
        }
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

    /// <summary>
    /// 判断 <see cref="DbUpdateException"/> 是否为唯一约束/唯一索引冲突（SQL Server 错误码 2601/2627），
    /// 兼容 PostgreSQL/MySQL 的错误消息关键字。仅此类冲突被视为"并发领取已存在"业务异常，
    /// 其他 DbUpdateException（连接失败、其他约束冲突）原样上抛由调用方处理。
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        var message = inner.Message ?? string.Empty;
        return message.Contains("2601", StringComparison.Ordinal)
            || message.Contains("2627", StringComparison.Ordinal)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<CouponDto?> GetByIdAsync(Guid couponId, CancellationToken ct = default)
    {
        var coupon = await _couponRepository.GetByIdAsync(couponId, ct);
        return coupon is null ? null : ToDto(coupon);
    }

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
