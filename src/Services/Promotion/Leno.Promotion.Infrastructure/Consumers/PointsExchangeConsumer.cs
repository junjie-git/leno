using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using Leno.SharedKernel.Abstractions;
using CouponAggregate = Leno.Promotion.Domain.Aggregates.Coupon;
using UserCouponAggregate = Leno.Promotion.Domain.Aggregates.UserCoupon;

namespace Leno.Promotion.Infrastructure.Consumers;

/// <summary>
/// 积分兑换优惠券消费者，验证模板有效性并创建用户券。
/// 通过 EventId 幂等去重（Redis 24h）+ ExchangeId 数据库唯一索引双重保障。
/// 重构（P1-3.2）：移除直接依赖 PromotionDbContext 手工写 OutboxMessage，
/// 改用聚合根 UserCoupon.RecordExchangeSucceeded 触发领域事件 →
/// IUnitOfWork.SaveEntitiesAsync 经 mapper 翻译为集成事件写入发件箱（同事务）→ ClearDomainEvents。
/// </summary>
public sealed class PointsExchangeConsumer : Leno.Infrastructure.EventBus.IntegrationEventConsumerBase<PointsExchangeCouponRequestedEvent>
{
    private readonly ICouponRepository _couponRepository;
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PointsExchangeConsumer(
        ICouponRepository couponRepository,
        IUserCouponRepository userCouponRepository,
        IUnitOfWork unitOfWork,
        ILogger<PointsExchangeConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(couponRepository);
        ArgumentNullException.ThrowIfNull(userCouponRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _couponRepository = couponRepository;
        _userCouponRepository = userCouponRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(PointsExchangeCouponRequestedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 0. 幂等防御：按 ExchangeId 查询是否已创建用户券（与基类 EventId 幂等存储互补）
        var existing = await _userCouponRepository.GetByExchangeIdAsync(integrationEvent.ExchangeId, ct);
        if (existing is not null)
        {
            Logger.LogInformation(
                "积分兑换已处理 ExchangeId={ExchangeId} UserCouponId={UserCouponId}，跳过",
                integrationEvent.ExchangeId, existing.Id);
            return;
        }

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

        // 4. 创建用户券实例并绑定兑换标识（聚合根内发布 CouponExchangeSucceededDomainEvent 领域事件）
        var expiredAt = coupon.ComputeExpiredAt(DateTime.UtcNow);
        var userCoupon = UserCouponAggregate.Receive(
            Guid.NewGuid(),
            integrationEvent.UserId,
            integrationEvent.CouponTemplateId,
            "PointsExchange",
            expiredAt,
            integrationEvent.ExchangeId);

        await _userCouponRepository.AddAsync(userCoupon, ct);

        // 5. 经 UnitOfWork 保存聚合变更 + 领域事件经 mapper 翻译为 CouponExchangeSucceededEvent
        //    集成事件写入发件箱（同事务），事务提交后 ClearDomainEvents
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation(
            "积分兑换成功 UserId={UserId} CouponId={CouponId} UserCouponId={UserCouponId} ExchangeId={ExchangeId}",
            integrationEvent.UserId, integrationEvent.CouponTemplateId, userCoupon.Id, integrationEvent.ExchangeId);
    }
}
