using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.PointsMembership.Infrastructure.Consumers;

/// <summary>
/// 退款完成事件消费者，扣回已发放的消费积分（允许余额为负）。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class RefundCompletedEventConsumer : RedisIntegrationEventConsumerBase<RefundCompletedEvent>
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RefundCompletedEventConsumer(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<RefundCompletedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(RefundCompletedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var account = await _accountRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
        if (account is null)
        {
            Logger.LogWarning("用户 {UserId} 积分账户不存在，跳过积分扣回", integrationEvent.UserId);
            return;
        }

        // 计算应扣回积分（1 元 = 1 积分）
        var pointsToRevert = (int)Math.Floor(integrationEvent.RefundAmount);
        if (pointsToRevert <= 0)
        {
            Logger.LogInformation("退款金额 {Amount} 无积分需扣回", integrationEvent.RefundAmount);
            return;
        }

        account.RevertPoints(pointsToRevert, integrationEvent.RefundId,
            $"退款 {integrationEvent.RefundId} 扣回已发放积分（订单 {integrationEvent.OrderId}）");

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("退款 {RefundId} 完成，已扣回 {Points} 积分，用户 {UserId} 余额 {Balance}",
            integrationEvent.RefundId, pointsToRevert, integrationEvent.UserId, account.Balance);
    }
}
