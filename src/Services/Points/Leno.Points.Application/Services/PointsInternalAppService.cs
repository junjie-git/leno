using Leno.Points.Application.DTOs;
using Leno.Points.Domain.Aggregates.PointsAccount;
using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using PointsAccountAggregate = Leno.Points.Domain.Aggregates.PointsAccount.PointsAccount;

namespace Leno.Points.Application.Services;

/// <summary>
/// 积分域内部应用服务实现，供其他 BC（如订单域、会员域）经 internal HTTP 端点调用。
/// 通过积分账户仓储操作聚合根，经工作单元在同事务内保存变更与发件箱事件。
/// 业务行为与旧域 PointsMembership.PointsInternalAppService 对齐，确保调用方零改造。
/// </summary>
public sealed class PointsInternalAppService : IPointsInternalAppService
{
    /// <summary>积分抵扣换算率：100 积分 = 1 元。</summary>
    private const int PointsPerYuan = 100;

    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PointsInternalAppService> _logger;

    public PointsInternalAppService(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<PointsInternalAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TrialOffsetResultDto> TrialOffsetAsync(Guid userId, decimal orderAmount, CancellationToken ct = default)
    {
        if (orderAmount < 0)
        {
            return new TrialOffsetResultDto { OffsetAmount = 0, UsedPoints = 0 };
        }

        var account = await _accountRepository.GetByUserIdAsync(userId, ct);
        if (account is null)
        {
            return new TrialOffsetResultDto { OffsetAmount = 0, UsedPoints = 0 };
        }

        // 按订单金额计算需要的积分（100 积分 = 1 元）
        var pointsNeeded = (int)Math.Ceiling(orderAmount * PointsPerYuan);
        if (pointsNeeded <= 0)
        {
            return new TrialOffsetResultDto { OffsetAmount = 0, UsedPoints = 0 };
        }

        // 实际可用积分不超过余额
        var usablePoints = Math.Min(pointsNeeded, account.Balance.Available);
        if (usablePoints <= 0)
        {
            return new TrialOffsetResultDto { OffsetAmount = 0, UsedPoints = 0 };
        }

        var offsetAmount = usablePoints / (decimal)PointsPerYuan;
        _logger.LogDebug(
            "试算积分抵扣 UserId={UserId} OrderAmount={OrderAmount} UsedPoints={UsedPoints} OffsetAmount={OffsetAmount}",
            userId, orderAmount, usablePoints, offsetAmount);

        return new TrialOffsetResultDto
        {
            OffsetAmount = offsetAmount,
            UsedPoints = usablePoints,
            Currency = "CNY"
        };
    }

    /// <inheritdoc />
    public async Task<FreezeResultDto> FreezeAsync(Guid userId, int points, Guid orderId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByUserIdAsync(userId, ct)
            ?? throw new PointsDomainException(
                $"用户 {userId} 的积分账户不存在",
                "POINTS_ACCOUNT_NOT_FOUND");

        account.Freeze(points, orderId);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "积分冻结成功 UserId={UserId} OrderId={OrderId} Points={Points}",
            userId, orderId, points);

        return new FreezeResultDto
        {
            Success = true,
            Points = points,
            OrderId = orderId,
            AccountId = account.Id,
            AvailableBalanceAfter = account.Balance.Available,
            FrozenBalanceAfter = account.Balance.Frozen
        };
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByFrozenOrderIdAsync(orderId, ct)
            ?? throw new PointsDomainException(
                $"订单 {orderId} 的冻结记录不存在",
                "POINTS_FROZEN_ENTRY_NOT_FOUND");

        account.Release(orderId);
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("积分释放成功 OrderId={OrderId}", orderId);
    }

    /// <inheritdoc />
    public async Task ConfirmAsync(Guid orderId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByFrozenOrderIdAsync(orderId, ct)
            ?? throw new PointsDomainException(
                $"订单 {orderId} 的冻结记录不存在",
                "POINTS_FROZEN_ENTRY_NOT_FOUND");

        account.ConfirmDeduct(orderId);
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("积分确认扣减成功 OrderId={OrderId}", orderId);
    }

    /// <inheritdoc />
    public async Task GrantLevelBonusAsync(Guid userId, int amount, int newLevel, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByUserIdAsync(userId, ct)
            ?? throw new PointsDomainException(
                $"用户 {userId} 的积分账户不存在",
                "POINTS_ACCOUNT_NOT_FOUND");

        account.GrantLevelBonus(amount, newLevel);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "等级提升奖励积分入账 UserId={UserId} NewLevel={NewLevel} Amount={Amount}",
            userId, newLevel, amount);
    }
}
