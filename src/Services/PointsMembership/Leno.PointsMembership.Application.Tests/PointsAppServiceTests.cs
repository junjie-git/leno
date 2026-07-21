using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.PointsMembership.Application.Tests;

public class PointsAppServiceTests
{
    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<ICheckInRecordRepository> _checkInRepoMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly PointsAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    public PointsAppServiceTests()
    {
        // PM-H01 修复后 PointsAppService 新增 IMemberRepository 依赖；
        // 既有测试不验证成长值累加，仅以默认 Mock 满足构造签名，行为由 PointsAppServiceCheckInGrowthTests 覆盖
        _sut = new PointsAppService(_accountRepoMock.Object, _checkInRepoMock.Object, _memberRepoMock.Object, _uowMock.Object);
    }

    private static PointsAccount CreateAccount()
    {
        return PointsAccount.Create(AccountId, UserId);
    }

    #region CheckInAsync

    [Fact]
    public async Task CheckInAsync_FirstTime_ShouldReturnBase10Points()
    {
        var account = CreateAccount();
        _checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        _checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.CheckInAsync(UserId);

        result.PointsAwarded.Should().Be(10);
        result.ContinuousDays.Should().Be(1);
        account.Balance.Should().Be(10);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckInAsync_AlreadyCheckedInToday_ShouldThrowException()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existingRecord = CheckInRecord.CheckIn(Guid.NewGuid(), UserId, today, 3, 10);
        _checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRecord);

        var act = () => _sut.CheckInAsync(UserId);

        await act.Should().ThrowAsync<PointsDomainException>().WithMessage("*今日已签到*");
    }

    [Fact]
    public async Task CheckInAsync_Consecutive7Days_ShouldReturn20Points()
    {
        var account = CreateAccount();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var yesterdayRecord = CheckInRecord.CheckIn(Guid.NewGuid(), UserId, yesterday, 6, 10);
        _checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        _checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yesterdayRecord);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.CheckInAsync(UserId);

        result.PointsAwarded.Should().Be(20);
        result.ContinuousDays.Should().Be(7);
    }

    [Fact]
    public async Task CheckInAsync_Consecutive30Days_ShouldReturn50Points()
    {
        var account = CreateAccount();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var yesterdayRecord = CheckInRecord.CheckIn(Guid.NewGuid(), UserId, yesterday, 29, 10);
        _checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        _checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yesterdayRecord);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.CheckInAsync(UserId);

        result.PointsAwarded.Should().Be(50);
        result.ContinuousDays.Should().Be(30);
    }

    [Fact]
    public async Task CheckInAsync_BreakStreak_ShouldResetTo1()
    {
        var account = CreateAccount();
        var twoDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);
        var oldRecord = CheckInRecord.CheckIn(Guid.NewGuid(), UserId, twoDaysAgo, 5, 10);
        _checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        _checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRecord);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.CheckInAsync(UserId);

        result.ContinuousDays.Should().Be(1);
        result.PointsAwarded.Should().Be(10);
    }

    #endregion

    #region GetPointsAccountAsync

    [Fact]
    public async Task GetPointsAccountAsync_Valid_ShouldReturnDto()
    {
        var account = CreateAccount();
        account.Earn(PointsSource.CheckIn, 50, "签到");
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.GetPointsAccountAsync(UserId);

        result.Should().NotBeNull();
        result.UserId.Should().Be(UserId);
        result.Balance.Should().Be(50);
        result.TotalEarned.Should().Be(50);
    }

    [Fact]
    public async Task GetPointsAccountAsync_NotFound_ShouldThrowException()
    {
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var act = () => _sut.GetPointsAccountAsync(UserId);

        await act.Should().ThrowAsync<PointsDomainException>().WithMessage("*不存在*");
    }

    #endregion

    #region GetLedgerAsync

    [Fact]
    public async Task GetLedgerAsync_ShouldReturnEmptyList()
    {
        var result = await _sut.GetLedgerAsync(UserId, 1, 20);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region AwardPointsAsync

    [Fact]
    public async Task AwardPointsAsync_Valid_ShouldAwardPoints()
    {
        var account = CreateAccount();
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var dto = new AwardPointsDto { UserId = UserId, Amount = 100, Reason = "活动奖励" };

        await _sut.AwardPointsAsync(dto);

        account.Balance.Should().Be(100);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AwardPointsAsync_AccountNotFound_ShouldThrowException()
    {
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);
        var dto = new AwardPointsDto { UserId = UserId, Amount = 100, Reason = "活动奖励" };

        var act = () => _sut.AwardPointsAsync(dto);

        await act.Should().ThrowAsync<PointsDomainException>().WithMessage("*不存在*");
    }

    #endregion
}