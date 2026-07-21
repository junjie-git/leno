using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 验证 PM-M03 修复：<see cref="PointsAppService.CheckInAsync"/> 使用配置的默认用户时区
/// （Asia/Shanghai）计算签到日期，而非 UTC，避免跨日签到错位。
/// </summary>
public sealed class PointsAppServiceTimeZoneTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public async Task CheckInAsync_Should_Use_Configured_TimeZone_For_Today()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var checkInRepoMock = new Mock<ICheckInRecordRepository>();
        checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        checkInRepoMock.Setup(r => r.AddAsync(It.IsAny<CheckInRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var memberRepoMock = new Mock<IMemberRepository>();

        // 配置 Asia/Shanghai 时区
        var options = Options.Create(new PointsMembershipOptions { DefaultTimeZone = "Asia/Shanghai" });

        var service = new PointsAppService(
            accountRepoMock.Object,
            checkInRepoMock.Object,
            memberRepoMock.Object,
            uowMock.Object,
            options);

        await service.CheckInAsync(UserId, CancellationToken.None);

        // 验证传给仓储的"今日"日期与 Asia/Shanghai 时区一致
        var shanghaiTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var expectedToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, shanghaiTz));

        checkInRepoMock.Verify(
            r => r.GetByUserIdAndDateAsync(UserId, expectedToday, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckInAsync_Should_Fallback_To_Utc_When_TimeZone_Invalid()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var checkInRepoMock = new Mock<ICheckInRecordRepository>();
        checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        checkInRepoMock.Setup(r => r.AddAsync(It.IsAny<CheckInRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var memberRepoMock = new Mock<IMemberRepository>();

        // 配置无效时区，应回退 UTC
        var options = Options.Create(new PointsMembershipOptions { DefaultTimeZone = "Invalid/Timezone" });

        var service = new PointsAppService(
            accountRepoMock.Object,
            checkInRepoMock.Object,
            memberRepoMock.Object,
            uowMock.Object,
            options);

        await service.CheckInAsync(UserId, CancellationToken.None);

        var expectedToday = DateOnly.FromDateTime(DateTime.UtcNow);

        checkInRepoMock.Verify(
            r => r.GetByUserIdAndDateAsync(UserId, expectedToday, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckInAsync_Should_Default_To_Shanghai_When_Options_Null()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var checkInRepoMock = new Mock<ICheckInRecordRepository>();
        checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        checkInRepoMock.Setup(r => r.AddAsync(It.IsAny<CheckInRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var memberRepoMock = new Mock<IMemberRepository>();

        // 不传 options，应默认使用 Asia/Shanghai
        var service = new PointsAppService(
            accountRepoMock.Object,
            checkInRepoMock.Object,
            memberRepoMock.Object,
            uowMock.Object);

        await service.CheckInAsync(UserId, CancellationToken.None);

        var shanghaiTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var expectedToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, shanghaiTz));

        checkInRepoMock.Verify(
            r => r.GetByUserIdAndDateAsync(UserId, expectedToday, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
