using Leno.Points.Application.DTOs;
using Leno.Points.Domain.Aggregates.PointsAccount;
using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.Repositories;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using PointsAccountAggregate = Leno.Points.Domain.Aggregates.PointsAccount.PointsAccount;

namespace Leno.Points.Application.Services;

/// <summary>
/// 运营手动发放积分应用服务实现，对应 POST /api/admin/points/award 端点。
/// 校验账户存在与发放数量合法，调用聚合根 Earn 方法累加余额与累计获取。
/// </summary>
public sealed class AwardAppService : IAwardAppService
{
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AwardAppService> _logger;

    public AwardAppService(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<AwardAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AwardResultDto> AwardAsync(Guid userId, int amount, string reason, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("发放积分数量须大于 0", "POINTS_AWARD_AMOUNT_INVALID");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new PointsDomainException("发放原因不可为空", "POINTS_AWARD_REASON_EMPTY");
        }

        var account = await _accountRepository.GetByUserIdAsync(userId, ct)
            ?? throw new PointsDomainException(
                $"用户 {userId} 的积分账户不存在",
                "POINTS_ACCOUNT_NOT_FOUND");

        account.Earn(PointsSource.Activity, amount, reason);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "运营手动发放积分 UserId={UserId} Amount={Amount} Reason={Reason}",
            userId, amount, reason);

        return new AwardResultDto
        {
            AccountId = account.Id,
            UserId = userId,
            Amount = amount,
            AvailableBalanceAfter = account.Balance.Available,
            TotalEarnedAfter = account.Balance.TotalEarned
        };
    }
}
