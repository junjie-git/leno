using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 积分域内部操作应用服务单元测试，聚焦 <see cref="PointsInternalAppService.ConfirmAsync"/>
/// 支付成功核销冻结流程。
/// 范本：<see cref="PointsOffsetAppServiceTests.ConfirmDeductAsync_Valid_ShouldDeductFrozenAndSave"/>。
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

    [Fact]
    public async Task Confirm_FrozenRecordExists_CallsConfirmDeductAndSaves()
    {
        // Arrange：账户初始余额 300，先冻结 200（产生冻结明细），按订单反查返回该账户
        var account = CreateAccount(withBalance: 300);
        account.Freeze(200, OrderId);
        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var input = new ConfirmPointsDto(OrderId);

        // Act
        await _sut.ConfirmAsync(input);

        // Assert：冻结余额归零、累计消耗累加、工作单元保存一次
        account.FrozenBalance.Should().Be(0);
        account.Balance.Should().Be(100);
        account.TotalSpent.Should().Be(200);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_NoFrozenRecord_ThrowsPointsDomainException()
    {
        // Arrange：按订单反查返回 null
        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var input = new ConfirmPointsDto(OrderId);

        // Act & Assert：抛 PointsDomainException 且不调用保存
        var act = () => _sut.ConfirmAsync(input);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*冻结记录不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
