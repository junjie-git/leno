using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using CheckInRecordAggregate = Leno.PointsMembership.Domain.Aggregates.CheckInRecord;
using PointsAccountAggregate = Leno.PointsMembership.Domain.Aggregates.PointsAccount;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 积分管理应用服务实现，编排签到、积分余额查询、流水查询与运营手动发放用例。
/// </summary>
public sealed class PointsAppService : IPointsAppService
{
    private const int CheckInBasePoints = 10;
    private const int CheckInWeeklyBonus = 20;
    private const int CheckInMonthlyBonus = 50;

    private readonly IPointsAccountRepository _accountRepository;
    private readonly ICheckInRecordRepository _checkInRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PointsAppService(
        IPointsAccountRepository accountRepository,
        ICheckInRecordRepository checkInRepository,
        IMemberRepository memberRepository,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository;
        _checkInRepository = checkInRepository;
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<CheckInResultDto> CheckInAsync(Guid userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 当日重复签到校验
        var existing = await _checkInRepository.GetByUserIdAndDateAsync(userId, today, ct);
        if (existing is not null)
        {
            throw new PointsDomainException("今日已签到，不可重复签到", "CHECKIN_ALREADY");
        }

        // 计算连续签到天数：最近一次签到为昨日则累加，否则重置为 1
        var latest = await _checkInRepository.GetLatestByUserIdAsync(userId, ct);
        var continuousDays = latest is not null && latest.CheckInDate == today.AddDays(-1)
            ? latest.ContinuousDays + 1
            : 1;

        // 奖励积分：连续 30 天 50 分、连续 7 天 20 分、否则基础 10 分
        var pointsAwarded = continuousDays >= 30
            ? CheckInMonthlyBonus
            : continuousDays >= 7
                ? CheckInWeeklyBonus
                : CheckInBasePoints;

        var record = CheckInRecordAggregate.CheckIn(
            Guid.NewGuid(), userId, today, continuousDays, pointsAwarded);
        await _checkInRepository.AddAsync(record, ct);

        var account = await RequireAccountAsync(userId, ct);
        var checkInReason = $"每日签到（连续 {continuousDays} 天）";
        account.Earn(PointsSource.CheckIn, pointsAwarded, checkInReason);

        // PM-H01 修复：同步累加会员成长值（1 积分 = 1 成长值），打通 V0-V4 成长值等级体系
        var member = await _memberRepository.GetByUserIdAsync(userId, ct);
        if (member is not null)
        {
            member.AddGrowthValue(pointsAwarded, checkInReason);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);

        return new CheckInResultDto
        {
            RecordId = record.Id,
            UserId = record.UserId,
            CheckInDate = record.CheckInDate,
            ContinuousDays = record.ContinuousDays,
            PointsAwarded = record.PointsAwarded
        };
    }

    /// <inheritdoc />
    public async Task<PointsAccountDto> GetPointsAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await RequireAccountAsync(userId, ct);
        return ToDto(account);
    }

    /// <inheritdoc />
    public Task<List<PointsLedgerDto>> GetLedgerAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        // 流水查询需独立的 IPointsLedgerRepository，当前域尚未定义，暂返回空列表。
        return Task.FromResult(new List<PointsLedgerDto>());
    }

    /// <inheritdoc />
    public async Task AwardPointsAsync(AwardPointsDto dto, CancellationToken ct = default)
    {
        var account = await RequireAccountAsync(dto.UserId, ct);
        account.Earn(PointsSource.Activity, dto.Amount, dto.Reason);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task<PointsAccountAggregate> RequireAccountAsync(Guid userId, CancellationToken ct)
        => await _accountRepository.GetByUserIdAsync(userId, ct)
           ?? throw new PointsDomainException(
               $"用户 {userId} 的积分账户不存在",
               "POINTS_ACCOUNT_NOT_FOUND");

    private static PointsAccountDto ToDto(PointsAccountAggregate account)
        => new()
        {
            Id = account.Id,
            UserId = account.UserId,
            Balance = account.Balance,
            FrozenBalance = account.FrozenBalance,
            TotalEarned = account.TotalEarned,
            TotalSpent = account.TotalSpent,
            CreatedAt = account.CreatedAt
        };
}
