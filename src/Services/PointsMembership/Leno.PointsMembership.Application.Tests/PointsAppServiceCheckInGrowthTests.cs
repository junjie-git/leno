using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 验证 PM-H01 修复：<see cref="PointsAppService.CheckInAsync"/> 在发放签到返积分时
/// 同步累加 <see cref="Member.AddGrowthValue"/>（1 积分 = 1 成长值），打通 V0-V4 成长值等级体系。
/// </summary>
public sealed class PointsAppServiceCheckInGrowthTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<ICheckInRecordRepository> _checkInRepoMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly PointsAppService _service;

    public PointsAppServiceCheckInGrowthTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        _checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        _checkInRepoMock.Setup(r => r.AddAsync(It.IsAny<CheckInRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new PointsAppService(
            _accountRepoMock.Object,
            _checkInRepoMock.Object,
            _memberRepoMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task CheckInAsync_FirstTime_Should_Accumulate_10_GrowthValue_For_Member()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var member = Member.Create(MemberId, UserId);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var result = await _service.CheckInAsync(UserId, CancellationToken.None);

        // 首次签到 10 积分，应同步累加 10 成长值
        Assert.Equal(10, result.PointsAwarded);
        Assert.Equal(10, account.Balance);
        Assert.Equal(10, member.GrowthValue);
    }

    [Fact]
    public async Task CheckInAsync_Continuous7Days_Should_Accumulate_20_GrowthValue()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var member = Member.Create(MemberId, UserId);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var yesterdayRecord = CheckInRecord.CheckIn(Guid.NewGuid(), UserId, yesterday, 6, 10);

        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yesterdayRecord);

        var result = await _service.CheckInAsync(UserId, CancellationToken.None);

        // 连续 7 天签到 20 积分，应同步累加 20 成长值
        Assert.Equal(20, result.PointsAwarded);
        Assert.Equal(20, member.GrowthValue);
    }

    [Fact]
    public async Task CheckInAsync_Continuous30Days_Should_Accumulate_50_GrowthValue()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var member = Member.Create(MemberId, UserId);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var yesterdayRecord = CheckInRecord.CheckIn(Guid.NewGuid(), UserId, yesterday, 29, 10);

        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yesterdayRecord);

        var result = await _service.CheckInAsync(UserId, CancellationToken.None);

        // 连续 30 天签到 50 积分，应同步累加 50 成长值
        Assert.Equal(50, result.PointsAwarded);
        Assert.Equal(50, member.GrowthValue);
    }

    [Fact]
    public async Task CheckInAsync_Should_Skip_GrowthValue_When_Member_NotFound()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member?)null);

        // 不应抛异常（member null 时跳过 AddGrowthValue）
        var result = await _service.CheckInAsync(UserId, CancellationToken.None);

        Assert.Equal(10, result.PointsAwarded);
        Assert.Equal(10, account.Balance);
    }
}
