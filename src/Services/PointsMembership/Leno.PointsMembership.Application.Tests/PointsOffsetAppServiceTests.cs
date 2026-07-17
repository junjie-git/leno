using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 积分抵扣应用服务单元测试，覆盖试算、冻结、确认扣减与释放四个核心流程。
/// 抵扣换算：100 积分 = 1 元。
/// </summary>
public class PointsOffsetAppServiceTests
{
    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly PointsOffsetAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public PointsOffsetAppServiceTests()
    {
        _sut = new PointsOffsetAppService(_accountRepoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task TryOffsetAsync_AccountNotExist_ShouldReturnZero()
    {
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var result = await _sut.TryOffsetAsync(UserId, pointsToUse: 100);

        result.Should().Be(0m);
    }

    [Fact]
    public async Task TryOffsetAsync_SufficientBalance_ShouldReturnOffsetAmount()
    {
        var account = CreateAccount(withBalance: 1000);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.TryOffsetAsync(UserId, pointsToUse: 200);

        result.Should().Be(2m);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryOffsetAsync_InsufficientBalance_ShouldReturnZero()
    {
        var account = CreateAccount(withBalance: 50);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.TryOffsetAsync(UserId, pointsToUse: 100);

        result.Should().Be(0m);
    }

    [Fact]
    public async Task TryOffsetAsync_ZeroPoints_ShouldReturnZero()
    {
        var account = CreateAccount(withBalance: 1000);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.TryOffsetAsync(UserId, pointsToUse: 0);

        result.Should().Be(0m);
    }

    [Fact]
    public async Task FreezeAsync_AccountNotExist_ShouldThrow()
    {
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var act = () => _sut.FreezeAsync(UserId, OrderId, pointsToUse: 100);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*积分账户不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FreezeAsync_Valid_ShouldFreezeAndSave()
    {
        var account = CreateAccount(withBalance: 500);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.FreezeAsync(UserId, OrderId, pointsToUse: 200);

        account.Balance.Should().Be(300);
        account.FrozenBalance.Should().Be(200);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FreezeAsync_InsufficientBalance_ShouldThrow()
    {
        var account = CreateAccount(withBalance: 100);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var act = () => _sut.FreezeAsync(UserId, OrderId, pointsToUse: 200);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*积分余额不足*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmDeductAsync_OrderNotFound_ShouldThrow()
    {
        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var act = () => _sut.ConfirmDeductAsync(OrderId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*冻结记录不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmDeductAsync_Valid_ShouldDeductFrozenAndSave()
    {
        var account = CreateAccount(withBalance: 300);
        account.Freeze(200, OrderId);
        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.ConfirmDeductAsync(OrderId);

        account.FrozenBalance.Should().Be(0);
        account.Balance.Should().Be(100);
        account.TotalSpent.Should().Be(200);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_Valid_ShouldReleaseAndSave()
    {
        var account = CreateAccount(withBalance: 300);
        account.Freeze(200, OrderId);
        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.ReleaseAsync(OrderId);

        account.FrozenBalance.Should().Be(0);
        account.Balance.Should().Be(300);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_OrderNotFound_ShouldThrow()
    {
        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var act = () => _sut.ReleaseAsync(OrderId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*冻结记录不存在*");
    }

    private static PointsAccount CreateAccount(int withBalance)
    {
        var account = PointsAccount.Create(AccountId, UserId);
        if (withBalance > 0)
        {
            account.Earn(PointsSource.CheckIn, withBalance, "测试初始积分");
        }
        return account;
    }
}
