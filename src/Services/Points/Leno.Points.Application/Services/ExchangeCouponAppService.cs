using Leno.Points.Application.DTOs;
using Leno.Points.Domain.Aggregates.PointsAccount;
using Leno.Points.Domain.Aggregates.PointsExchange;
using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.Repositories;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using PointsAccountAggregate = Leno.Points.Domain.Aggregates.PointsAccount.PointsAccount;
using PointsExchangeAggregate = Leno.Points.Domain.Aggregates.PointsExchange.PointsExchange;

namespace Leno.Points.Application.Services;

/// <summary>
/// 积分兑换优惠券应用服务实现。
/// 调用 <see cref="PointsAccountAggregate.ConsumePoints"/> 在同一事务内扣减积分并创建兑换聚合，
/// 由发件箱模式翻译为 <c>PointsExchangeCouponRequestedEvent</c> 集成事件给优惠券域，
/// 保证扣减与事件发布的原子性。
/// </summary>
public sealed class ExchangeCouponAppService : IExchangeCouponAppService
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IPointsExchangeRepository _exchangeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExchangeCouponAppService> _logger;

    public ExchangeCouponAppService(
        IPointsAccountRepository accountRepository,
        IPointsExchangeRepository exchangeRepository,
        IUnitOfWork unitOfWork,
        ILogger<ExchangeCouponAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(exchangeRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _accountRepository = accountRepository;
        _exchangeRepository = exchangeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExchangeCouponResultDto> ExchangeAsync(Guid userId, Guid couponTemplateId, int pointsRequired, CancellationToken ct = default)
    {
        if (pointsRequired <= 0)
        {
            throw new PointsDomainException("兑换积分数量须大于 0", "POINTS_EXCHANGE_AMOUNT_INVALID");
        }

        var account = await _accountRepository.GetByUserIdAsync(userId, ct)
            ?? throw new PointsDomainException(
                $"用户 {userId} 的积分账户不存在",
                "POINTS_ACCOUNT_NOT_FOUND");

        if (account.Balance.Available < pointsRequired)
        {
            throw new PointsDomainException(
                $"积分余额不足：可用 {account.Balance.Available}，兑换需要 {pointsRequired}",
                "POINTS_BALANCE_INSUFFICIENT");
        }

        var exchangeId = Guid.NewGuid();

        // 创建兑换聚合（初始 Pending 状态，等待优惠券域确认）
        var exchange = PointsExchangeAggregate.Create(
            exchangeId,
            userId,
            account.Id,
            couponTemplateId,
            ExchangeType.CouponExchange,
            pointsRequired);

        // 同事务：扣减积分 + 创建兑换聚合（领域事件经 Outbox 翻译为集成事件）
        account.ConsumePoints(pointsRequired, exchangeId, $"积分兑换优惠券-{couponTemplateId}");
        await _exchangeRepository.AddAsync(exchange, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "积分兑换优惠券请求已提交 ExchangeId={ExchangeId} UserId={UserId} Points={Points}",
            exchangeId, userId, pointsRequired);

        return new ExchangeCouponResultDto
        {
            ExchangeId = exchangeId,
            UserId = userId,
            CouponTemplateId = couponTemplateId,
            PointsFrozen = pointsRequired,
            Status = "Pending"
        };
    }
}
