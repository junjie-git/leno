using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.PointsMembership.Infrastructure.Consumers;

/// <summary>
/// 订单完成事件消费者，发放消费返积分、累加会员消费金额并检查升级。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class OrderCompletedEventConsumer : IntegrationEventConsumerBase<OrderCompletedEvent>
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IMembershipLevelRepository _levelRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderCompletedEventConsumer(
        IPointsAccountRepository accountRepository,
        IMemberRepository memberRepository,
        IMembershipLevelRepository levelRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderCompletedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _accountRepository = accountRepository;
        _memberRepository = memberRepository;
        _levelRepository = levelRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderCompletedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // PM-H07 修复：消费返积分与消费金额累加统一由 OrderAfterSalesWindowClosedEventConsumer
        // 在售后窗口关闭后发放，避免订单完成与售后窗口关闭双事件触发导致同一订单双倍发放积分
        // 与消费金额翻倍，以及退货后已发积分难追回的问题。本消费者仅记录日志，
        // 便于后续若需触发"订单完成通知"等下游事件时扩展。
        await Task.CompletedTask;

        Logger.LogInformation(
            "订单 {OrderId} 已完成，消费返积分将在售后窗口关闭后由 OrderAfterSalesWindowClosedEventConsumer 发放",
            integrationEvent.OrderId);
    }
}

/// <summary>
/// 订单取消事件消费者，释放冻结的抵现积分。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class OrderCancelledEventConsumer : IntegrationEventConsumerBase<OrderCancelledEvent>
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderCancelledEventConsumer(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderCancelledEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderCancelledEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var account = await _accountRepository.GetByFrozenOrderIdAsync(integrationEvent.OrderId, ct);
        if (account is null)
        {
            Logger.LogInformation("订单 {OrderId} 无冻结积分，跳过释放", integrationEvent.OrderId);
            return;
        }

        account.Release(integrationEvent.OrderId);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 取消，已释放冻结积分给用户 {UserId}",
            integrationEvent.OrderId, account.UserId);
    }
}

/// <summary>
/// 订单售后窗口关闭事件消费者，在售后窗口关闭后发放消费返积分并累加会员消费金额。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class OrderAfterSalesWindowClosedEventConsumer : IntegrationEventConsumerBase<OrderAfterSalesWindowClosedEvent>
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IMembershipLevelRepository _levelRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderAfterSalesWindowClosedEventConsumer(
        IPointsAccountRepository accountRepository,
        IMemberRepository memberRepository,
        IMembershipLevelRepository levelRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderAfterSalesWindowClosedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _accountRepository = accountRepository;
        _memberRepository = memberRepository;
        _levelRepository = levelRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderAfterSalesWindowClosedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 消费返积分：1 元 = 1 积分
        var points = (int)Math.Floor(integrationEvent.PaidAmount);
        if (points > 0)
        {
            var account = await _accountRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
            if (account is not null)
            {
                account.Earn(PointsSource.Consumption, points, $"消费返积分-订单{integrationEvent.OrderId}");
            }
        }

        // 累加会员消费金额并检查升级
        if (integrationEvent.PaidAmount > 0)
        {
            var member = await _memberRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
            if (member is not null)
            {
                // PM-H01 修复：1 积分 = 1 成长值，同步累加成长值打通 V0-V4 等级体系
                if (points > 0)
                {
                    member.AddGrowthValue(points, $"订单 {integrationEvent.OrderId} 消费返积分");
                }
                member.AddConsumption(integrationEvent.PaidAmount);

                var levels = await _levelRepository.GetAllEnabledAsync(ct);
                var thresholds = levels
                    .Select(l => new LevelThreshold(l.Level, l.Name, l.MinConsumption))
                    .ToList();
                member.CheckUpgrade(thresholds);
            }
        }

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 售后窗口关闭，发放 {Points} 消费积分给用户 {UserId}",
            integrationEvent.OrderId, points, integrationEvent.UserId);
    }
}
