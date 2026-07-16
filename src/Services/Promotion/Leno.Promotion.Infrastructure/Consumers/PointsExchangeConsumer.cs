﻿using Leno.Infrastructure.Outbox;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using CouponAggregate = Leno.Promotion.Domain.Aggregates.Coupon;
using UserCouponAggregate = Leno.Promotion.Domain.Aggregates.UserCoupon;

namespace Leno.Promotion.Infrastructure.Consumers;

/// <summary>
/// 积分兑换优惠券消费者，验证模板有效性并创建用户券。
/// 通过 EventId 幂等去重（Redis 24h）。
/// </summary>
public sealed class PointsExchangeConsumer : Leno.Infrastructure.EventBus.IntegrationEventConsumerBase<PointsExchangeCouponRequestedEvent>
{
    private readonly ICouponRepository _couponRepository;
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly PromotionDbContext _dbContext;

    public PointsExchangeConsumer(
        ICouponRepository couponRepository,
        IUserCouponRepository userCouponRepository,
        PromotionDbContext dbContext,
        ILogger<PointsExchangeConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(couponRepository);
        ArgumentNullException.ThrowIfNull(userCouponRepository);
        ArgumentNullException.ThrowIfNull(dbContext);
        _couponRepository = couponRepository;
        _userCouponRepository = userCouponRepository;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(PointsExchangeCouponRequestedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 1. 校验券模板存在且启用
        var coupon = await _couponRepository.GetByIdAsync(integrationEvent.CouponTemplateId, ct);
        if (coupon is null)
        {
            Logger.LogWarning("积分兑换失败：券模板 {CouponId} 不存在", integrationEvent.CouponTemplateId);
            return;
        }

        if (coupon.Status != CouponTemplateStatus.Enabled)
        {
            Logger.LogWarning("积分兑换失败：券模板 {CouponId} 已禁用", integrationEvent.CouponTemplateId);
            return;
        }

        // 2. 校验可领取
        if (!coupon.IsReceivable(DateTime.UtcNow))
        {
            Logger.LogWarning("积分兑换失败：券模板 {CouponId} 不可领取（已停用/已过期/已发完）", integrationEvent.CouponTemplateId);
            return;
        }

        // 3. 发放券模板
        coupon.Issue(1);

        // 4. 创建用户券实例
        var expiredAt = coupon.ComputeExpiredAt(DateTime.UtcNow);
        var userCoupon = UserCouponAggregate.Receive(
            Guid.NewGuid(),
            integrationEvent.UserId,
            integrationEvent.CouponTemplateId,
            "PointsExchange",
            expiredAt);

        await _userCouponRepository.AddAsync(userCoupon, ct);

        // 5. 发布兑换成功事件（直接写入发件箱）
        var exchangeSucceededEvent = new CouponExchangeSucceededEvent(
            integrationEvent.ExchangeId,
            integrationEvent.UserId,
            userCoupon.Id);
        _dbContext.OutboxMessages.Add(OutboxMessage.Create(exchangeSucceededEvent));

        await _dbContext.SaveChangesAsync(ct);

        Logger.LogInformation(
            "积分兑换成功 UserId={UserId} CouponId={CouponId} UserCouponId={UserCouponId} ExchangeId={ExchangeId}",
            integrationEvent.UserId, integrationEvent.CouponTemplateId, userCoupon.Id, integrationEvent.ExchangeId);
    }
}