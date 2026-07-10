using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.Services;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

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
        ILogger<OrderCompletedEventConsumer> logger)
        : base(logger)
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

        // 消费返积分：1 元 = 1 积分（简化规则）
        var points = (int)Math.Floor(integrationEvent.TotalAmount);
        if (points > 0)
        {
            var account = await _accountRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
            if (account is not null)
            {
                account.Earn(PointsSource.Consumption, points, $"订单 {integrationEvent.OrderId} 消费返积分");
            }
        }

        // 累加会员消费金额并检查升级
        var member = await _memberRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
        if (member is not null)
        {
            member.AddConsumption(integrationEvent.TotalAmount);

            var levels = await _levelRepository.GetAllEnabledAsync(ct);
            var thresholds = levels
                .Select(l => new LevelThreshold(l.Level, l.Name, l.MinConsumption))
                .ToList();
            member.CheckUpgrade(thresholds);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 完成，发放 {Points} 消费积分给用户 {UserId}",
            integrationEvent.OrderId, points, integrationEvent.UserId);
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
        ILogger<OrderCancelledEventConsumer> logger)
        : base(logger)
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
