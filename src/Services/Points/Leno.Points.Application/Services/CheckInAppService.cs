using Leno.Points.Application.DTOs;
using Leno.Points.Domain.Aggregates.CheckInRecord;
using Leno.Points.Domain.Aggregates.PointsAccount;
using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.Repositories;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using CheckInRecordAggregate = Leno.Points.Domain.Aggregates.CheckInRecord.CheckInRecord;
using PointsAccountAggregate = Leno.Points.Domain.Aggregates.PointsAccount.PointsAccount;

namespace Leno.Points.Application.Services;

/// <summary>
/// 签到应用服务实现，编排每日签到用例。
/// 计算连续签到天数与奖励积分，发放积分到账户。
/// 签到日期计算使用配置的默认用户时区（Asia/Shanghai），避免 UTC 跨日导致签到错位。
/// </summary>
public sealed class CheckInAppService : ICheckInAppService
{
    /// <summary>基础签到奖励积分。</summary>
    private const int CheckInBasePoints = 10;

    /// <summary>连续 7 天奖励积分。</summary>
    private const int CheckInWeeklyBonus = 20;

    /// <summary>连续 30 天奖励积分。</summary>
    private const int CheckInMonthlyBonus = 50;

    /// <summary>默认用户时区标识。</summary>
    private const string DefaultTimeZoneId = "Asia/Shanghai";

    private readonly IPointsAccountRepository _accountRepository;
    private readonly ICheckInRecordRepository _checkInRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CheckInAppService> _logger;
    private readonly TimeZoneInfo _userTimeZone;

    public CheckInAppService(
        IPointsAccountRepository accountRepository,
        ICheckInRecordRepository checkInRepository,
        IUnitOfWork unitOfWork,
        ILogger<CheckInAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(checkInRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _accountRepository = accountRepository;
        _checkInRepository = checkInRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;

        _userTimeZone = TimeZoneInfo.TryFindSystemTimeZoneById(DefaultTimeZoneId, out var tz)
            ? tz
            : TimeZoneInfo.Utc;
    }

    /// <inheritdoc />
    public async Task<CheckInResultDto> CheckInAsync(Guid userId, CancellationToken ct = default)
    {
        // 使用用户时区计算"今日"，避免 UTC 跨日导致签到错位
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

        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "签到成功 UserId={UserId} CheckInDate={CheckInDate} ContinuousDays={ContinuousDays} PointsAwarded={PointsAwarded}",
            userId, today, continuousDays, pointsAwarded);

        return new CheckInResultDto
        {
            RecordId = record.Id,
            UserId = record.UserId,
            CheckInDate = record.CheckInDate,
            ContinuousDays = record.ContinuousDays,
            PointsAwarded = record.PointsAwarded
        };
    }

    private async Task<PointsAccountAggregate> RequireAccountAsync(Guid userId, CancellationToken ct)
        => await _accountRepository.GetByUserIdAsync(userId, ct)
           ?? throw new PointsDomainException(
               $"用户 {userId} 的积分账户不存在",
               "POINTS_ACCOUNT_NOT_FOUND");
}
