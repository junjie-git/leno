using Leno.Infrastructure.EventBus;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.Promotion.Infrastructure.Consumers;

/// <summary>
/// 订单支付成功事件消费者，核销锁定的用户优惠券（UserCoupon.Consume）。
/// 通过 EventId 幂等去重；券不存在或非 Locked 态时跳过（该订单未使用优惠券）。
/// </summary>
public sealed class OrderPaidEventConsumer : IntegrationEventConsumerBase<OrderPaidEvent>
{
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderPaidEventConsumer(
        IUserCouponRepository userCouponRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderPaidEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(userCouponRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _userCouponRepository = userCouponRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderPaidEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 按锁定订单定位券；幂等校验：若已核销则跳过
        var alreadyUsed = await _userCouponRepository.GetByUsedOrderIdAsync(integrationEvent.OrderId, ct);
        if (alreadyUsed is not null)
        {
            Logger.LogInformation("订单 {OrderId} 的优惠券已核销，跳过重复处理", integrationEvent.OrderId);
            return;
        }

        var userCoupon = await _userCouponRepository.GetByLockedOrderIdAsync(integrationEvent.OrderId, ct);
        if (userCoupon is null)
        {
            Logger.LogInformation("订单 {OrderId} 未绑定优惠券，跳过核销", integrationEvent.OrderId);
            return;
        }

        userCoupon.Consume(integrationEvent.OrderId);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 已核销用户券 {UserCouponId}",
            integrationEvent.OrderId, userCoupon.Id);
    }
}

/// <summary>
/// 订单取消事件消费者，退还锁定的用户优惠券（UserCoupon.Release）。
/// 通过 EventId 幂等去重；券不存在或非 Locked 态时跳过。
/// </summary>
public sealed class OrderCancelledEventConsumer : IntegrationEventConsumerBase<OrderCancelledEvent>
{
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderCancelledEventConsumer(
        IUserCouponRepository userCouponRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderCancelledEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(userCouponRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _userCouponRepository = userCouponRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderCancelledEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var userCoupon = await _userCouponRepository.GetByLockedOrderIdAsync(integrationEvent.OrderId, ct);
        if (userCoupon is null)
        {
            Logger.LogInformation("订单 {OrderId} 未绑定优惠券，跳过退还", integrationEvent.OrderId);
            return;
        }

        // 状态前置检查：券已核销（Used）说明订单已支付后又被取消（如退款流程触发取消事件），
        // 应由 RefundCompletedEventConsumer 走 Return 流程退还，此处不应调 Release（会抛 USER_COUPON_RELEASE_INVALID 进死信）
        if (userCoupon.Status == CouponStatus.Used)
        {
            Logger.LogInformation(
                "订单 {OrderId} 的券 {UserCouponId} 已核销（Used），跳过 Release（应由 RefundCompleted 退还）",
                integrationEvent.OrderId, userCoupon.Id);
            return;
        }

        // 防御性：其他非 Locked 状态（如已 Expired）幂等跳过，避免 MassTransit 死信
        if (userCoupon.Status != CouponStatus.Locked)
        {
            Logger.LogInformation(
                "订单 {OrderId} 券状态 {Status} 非 Locked，幂等跳过",
                integrationEvent.OrderId, userCoupon.Status);
            return;
        }

        userCoupon.Release();
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 已退还用户券 {UserCouponId}",
            integrationEvent.OrderId, userCoupon.Id);
    }
}

/// <summary>
/// 退款完成事件消费者，退还已核销的用户优惠券（UserCoupon.Return）。
/// 通过 EventId 幂等去重；券不存在或非 Used 态时跳过。
/// </summary>
public sealed class RefundCompletedEventConsumer : IntegrationEventConsumerBase<RefundCompletedEvent>
{
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RefundCompletedEventConsumer(
        IUserCouponRepository userCouponRepository,
        IUnitOfWork unitOfWork,
        ILogger<RefundCompletedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(userCouponRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _userCouponRepository = userCouponRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(RefundCompletedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var userCoupon = await _userCouponRepository.GetByUsedOrderIdAsync(integrationEvent.OrderId, ct);
        if (userCoupon is null)
        {
            Logger.LogInformation("订单 {OrderId} 未绑定已核销优惠券，跳过退还", integrationEvent.OrderId);
            return;
        }

        userCoupon.Return();
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("退款完成，订单 {OrderId} 已退还用户券 {UserCouponId}",
            integrationEvent.OrderId, userCoupon.Id);
    }
}