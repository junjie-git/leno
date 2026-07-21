using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Options;
using CheckInRecordAggregate = Leno.PointsMembership.Domain.Aggregates.CheckInRecord;
using PointsAccountAggregate = Leno.PointsMembership.Domain.Aggregates.PointsAccount;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 积分管理应用服务实现，编排签到、积分余额查询、流水查询与运营手动发放用例。
/// PM-M03 修复：签到日期计算改用配置的默认用户时区（Asia/Shanghai），避免 UTC 跨日导致签到错位。
/// </summary>
public sealed class PointsAppService : IPointsAppService
{
    private const int CheckInBasePoints = 10;
    private const int CheckInWeeklyBonus = 20;
    private const int CheckInMonthlyBonus = 50;
    private const string DefaultTimeZoneId = "Asia/Shanghai";

    private readonly IPointsAccountRepository _accountRepository;
    private readonly ICheckInRecordRepository _checkInRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _userTimeZone;

    public PointsAppService(
        IPointsAccountRepository accountRepository,
        ICheckInRecordRepository checkInRepository,
        IMemberRepository memberRepository,
        IUnitOfWork unitOfWork,
        IOptions<PointsMembershipOptions>? options = null)
    {
        _accountRepository = accountRepository;
        _checkInRepository = checkInRepository;
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;

        // PM-M03 修复：从配置读取默认用户时区，解析失败时回退 Asia/Shanghai
        var timeZoneId = options?.Value.DefaultTimeZone ?? DefaultTimeZoneId;
        _userTimeZone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var tz)
            ? tz
            : TimeZoneInfo.Utc;
    }

    /// <inheritdoc />
    public async Task<CheckInResultDto> CheckInAsync(Guid userId, CancellationToken ct = default)
    {
        // PM-M03 修复：使用用户时区计算"今日"，避免 UTC 跨日导致用户在前一天 23:30 签到时被记为次日
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _userTimeZone));

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
    public async Task<List<PointsLedgerDto>> GetLedgerAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        // PM-M07 修复：实现真实分页查询，按发生时间倒序返回积分流水
        // 分页参数边界保护：page < 1 视为第 1 页，pageSize < 1 默认 20，pageSize > 100 上限 100
        if (page < 1)
        {
            page = 1;
        }
        if (pageSize < 1)
        {
            pageSize = 20;
        }
        if (pageSize > 100)
        {
            pageSize = 100;
        }

        var ledgers = await _accountRepository.GetLedgersByUserIdAsync(userId, page, pageSize, ct);
        return (ledgers ?? new List<PointsLedger>()).Select(ToDto).ToList();
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

    private static PointsLedgerDto ToDto(PointsLedger ledger)
        => new()
        {
            Id = ledger.Id,
            AccountId = ledger.AccountId,
            TxType = ledger.TxType,
            Amount = ledger.Amount,
            BalanceAfter = ledger.BalanceAfter,
            Source = ledger.Source,
            ReferenceId = ledger.ReferenceId,
            Reason = ledger.Reason,
            OccurredAt = ledger.OccurredAt
        };
}
