using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using PointsAccountAggregate = Leno.PointsMembership.Domain.Aggregates.PointsAccount;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 积分兑换优惠券应用服务实现。
/// 冻结积分后发布 PointsExchangeCouponRequestedEvent 给优惠券域。
/// </summary>
public sealed class ExchangeCouponAppService : IExchangeCouponAppService
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ExchangeCouponAppService> _logger;

    public ExchangeCouponAppService(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        IEventBus eventBus,
        ILogger<ExchangeCouponAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(logger);
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<ExchangeCouponResultDto> ExchangeCouponAsync(ExchangeCouponDto input, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByUserIdAsync(input.UserId, ct)
            ?? throw new PointsDomainException(
                $"用户 {input.UserId} 的积分账户不存在",
                "POINTS_ACCOUNT_NOT_FOUND",
                404);

        if (account.Balance < input.PointsRequired)
        {
            throw new PointsDomainException(
                $"积分余额不足：可用 {account.Balance}，兑换需要 {input.PointsRequired}",
                "POINTS_BALANCE_INSUFFICIENT");
        }

        var exchangeId = Guid.NewGuid();

        // 冻结积分（使用兑换ID作为订单ID）
        account.Freeze(input.PointsRequired, exchangeId);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 发布积分兑换优惠券请求事件
        var evt = new PointsExchangeCouponRequestedEvent(
            exchangeId, input.UserId, input.CouponTemplateId, input.PointsRequired);
        await _eventBus.PublishAsync(evt, ct);

        _logger.LogInformation(
            "积分兑换优惠券请求已提交 ExchangeId={ExchangeId} UserId={UserId} Points={Points}",
            exchangeId, input.UserId, input.PointsRequired);

        return new ExchangeCouponResultDto
        {
            ExchangeId = exchangeId,
            UserId = input.UserId,
            CouponTemplateId = input.CouponTemplateId,
            PointsFrozen = input.PointsRequired,
            Status = "Pending"
        };
    }
}
