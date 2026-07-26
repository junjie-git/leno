using Leno.Points.Application.DTOs;
using Leno.Points.Application.Services;
using Leno.Points.Domain.Aggregates.PointsAccount;
using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.Repositories;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PointsAccountAggregate = Leno.Points.Domain.Aggregates.PointsAccount.PointsAccount;

namespace Leno.Points.Application.Tests;

/// <summary>
/// 积分域内部应用服务单元测试，覆盖 <see cref="PointsInternalAppService"/> 的 4 个 internal 方法。
/// 验证试算抵扣、冻结、释放、确认扣减的业务行为与旧域 PointsMembership 对齐。
/// </summary>
public class PointsInternalAppServiceTests
{
    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly PointsInternalAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public PointsInternalAppServiceTests()
    {
        _sut = new PointsInternalAppService(
            _accountRepoMock.Object,
            _uowMock.Object,
            NullLogger<PointsInternalAppService>.Instance);
    }

    /// <summary>
    /// 试算抵扣：账户余额 1000，订单金额 100 元，应返回抵扣金额 > 0 且使用积分 <= 1000。
    /// 100 元需要 10000 积分，但余额仅 1000，故使用全部 1000 积分抵扣 10 元。
    /// </summary>
    [Fact]
    public async Task TrialOffsetAsync_WithValidInputs_ReturnsOffsetResult()
    {
        // Arrange：账户余额 1000
        var account = CreateAccount(withBalance: 1000);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act：订单金额 100 元
        var result = await _sut.TrialOffsetAsync(UserId, 100m, CancellationToken.None);

        // Assert：抵扣金额 > 0，使用积分 <= 1000
        result.Should().NotBeNull();
        result.OffsetAmount.Should().BeGreaterThan(0);
        result.UsedPoints.Should().BeLessThanOrEqualTo(1000);
        result.UsedPoints.Should().Be(1000);
        result.OffsetAmount.Should().Be(10m);
        result.Currency.Should().Be("CNY");
    }

    /// <summary>
    /// 冻结积分：账户余额 1000，冻结 500，应返回成功且余额减 500、冻结加 500。
    /// </summary>
    [Fact]
    public async Task FreezeAsync_WithSufficientBalance_ReturnsSuccess()
    {
        // Arrange：账户余额 1000
        var account = CreateAccount(withBalance: 1000);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act：冻结 500 积分
        var result = await _sut.FreezeAsync(UserId, 500, OrderId, CancellationToken.None);

        // Assert：返回成功，余额减 500，冻结加 500
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Points.Should().Be(500);
        result.OrderId.Should().Be(OrderId);
        result.AccountId.Should().Be(AccountId);
        result.AvailableBalanceAfter.Should().Be(500);
        result.FrozenBalanceAfter.Should().Be(500);

        account.Balance.Available.Should().Be(500);
        account.Balance.Frozen.Should().Be(500);

        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 冻结积分：账户余额 100，冻结 500，应抛 PointsDomainException（余额不足）。
    /// </summary>
    [Fact]
    public async Task FreezeAsync_WithInsufficientBalance_ThrowsDomainException()
    {
        // Arrange：账户余额仅 100
        var account = CreateAccount(withBalance: 100);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act & Assert：冻结 500 应抛 PointsDomainException
        var act = () => _sut.FreezeAsync(UserId, 500, OrderId, CancellationToken.None);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*余额不足*");

        // 不应调用保存
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 释放冻结：账户已冻结 500（余额 500、冻结 500），释放后余额恢复 1000、冻结归零。
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_WithFrozenOrder_ReturnsPoints()
    {
        // Arrange：账户初始余额 1000，先冻结 500
        var account = CreateAccount(withBalance: 1000);
        account.Freeze(500, OrderId);
        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // 验证冻结后状态
        account.Balance.Available.Should().Be(500);
        account.Balance.Frozen.Should().Be(500);

        // Act：释放冻结
        await _sut.ReleaseAsync(OrderId, CancellationToken.None);

        // Assert：余额恢复 1000，冻结归零
        account.Balance.Available.Should().Be(1000);
        account.Balance.Frozen.Should().Be(0);

        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 确认扣减：账户已冻结 500（余额 500、冻结 500），确认后冻结归零、可用余额不变（已扣减）、累计消耗累加。
    /// </summary>
    [Fact]
    public async Task ConfirmAsync_WithFrozenOrder_ConfirmsDeduction()
    {
        // Arrange：账户初始余额 1000，先冻结 500
        var account = CreateAccount(withBalance: 1000);
        account.Freeze(500, OrderId);
        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // 验证冻结后状态
        account.Balance.Available.Should().Be(500);
        account.Balance.Frozen.Should().Be(500);
        account.Balance.TotalSpent.Should().Be(0);

        // Act：确认扣减
        await _sut.ConfirmAsync(OrderId, CancellationToken.None);

        // Assert：冻结归零，可用余额不变（仍为 500，已扣减不回退），累计消耗累加 500
        account.Balance.Available.Should().Be(500);
        account.Balance.Frozen.Should().Be(0);
        account.Balance.TotalSpent.Should().Be(500);

        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 创建测试用积分账户并预置初始余额。
    /// </summary>
    private static PointsAccountAggregate CreateAccount(int withBalance)
    {
        var account = PointsAccountAggregate.Create(AccountId, UserId);
        if (withBalance > 0)
        {
            account.Earn(PointsSource.CheckIn, withBalance, "测试初始积分");
        }
        return account;
    }
}
