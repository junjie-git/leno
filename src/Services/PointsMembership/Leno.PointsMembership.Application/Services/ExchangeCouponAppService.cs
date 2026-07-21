using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using PointsAccountAggregate = Leno.PointsMembership.Domain.Aggregates.PointsAccount;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 积分兑换优惠券应用服务实现。
/// 调用 <see cref="PointsAccountAggregate.RequestExchangeCoupon"/> 在同一事务内冻结积分并追加领域事件，
/// 由发件箱模式翻译为 <c>PointsExchangeCouponRequestedEvent</c> 集成事件给优惠券域，
/// 保证冻结与事件发布的原子性（不再走 SaveEntities 之后的 IEventBus.PublishAsync 直发）。
/// </summary>
public sealed class ExchangeCouponAppService : IExchangeCouponAppService
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExchangeCouponAppService> _logger;

    public ExchangeCouponAppService(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<ExchangeCouponAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ExchangeCouponResultDto> ExchangeCouponAsync(ExchangeCouponDto input, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByUserIdAsync(input.UserId, ct)
            ?? throw new PointsDomainException(
                $"用户 {input.UserId} 的积分账户不存在",
                "POINTS_ACCOUNT_NOT_FOUND");

        if (account.Balance < input.PointsRequired)
        {
            throw new PointsDomainException(
                $"积分余额不足：可用 {account.Balance}，兑换需要 {input.PointsRequired}",
                "POINTS_BALANCE_INSUFFICIENT");
        }

        var exchangeId = Guid.NewGuid();

        // 聚合根内同事务：冻结积分 + 追加兑换请求领域事件（经 Outbox 翻译为集成事件）
        account.RequestExchangeCoupon(input.PointsRequired, exchangeId, input.CouponTemplateId);
        await _unitOfWork.SaveEntitiesAsync(ct);

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
