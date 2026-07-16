using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.PointsMembership.Infrastructure.Consumers;

/// <summary>
/// 优惠券兑换成功事件消费者，正式扣减冻结积分。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class CouponExchangeSucceededEventConsumer : IntegrationEventConsumerBase<CouponExchangeSucceededEvent>
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CouponExchangeSucceededEventConsumer(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<CouponExchangeSucceededEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(CouponExchangeSucceededEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var account = await _accountRepository.GetByFrozenOrderIdAsync(integrationEvent.ExchangeId, ct);
        if (account is null)
        {
            Logger.LogWarning("兑换 {ExchangeId} 无冻结积分，跳过确认扣减", integrationEvent.ExchangeId);
            return;
        }

        account.ConfirmDeduct(integrationEvent.ExchangeId);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("兑换 {ExchangeId} 成功，已确认扣减积分，优惠券 {CouponId}",
            integrationEvent.ExchangeId, integrationEvent.CouponId);
    }
}

/// <summary>
/// 优惠券兑换失败事件消费者，释放冻结积分。
/// 通过 EventId 幂等去重。
/// </summary>
public sealed class CouponExchangeFailedEventConsumer : IntegrationEventConsumerBase<CouponExchangeFailedEvent>
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CouponExchangeFailedEventConsumer(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<CouponExchangeFailedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(CouponExchangeFailedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var account = await _accountRepository.GetByFrozenOrderIdAsync(integrationEvent.ExchangeId, ct);
        if (account is null)
        {
            Logger.LogWarning("兑换 {ExchangeId} 无冻结积分，跳过释放", integrationEvent.ExchangeId);
            return;
        }

        account.Release(integrationEvent.ExchangeId);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("兑换 {ExchangeId} 失败（{Reason}），已释放冻结积分",
            integrationEvent.ExchangeId, integrationEvent.Reason);
    }
}
