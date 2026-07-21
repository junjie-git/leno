using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 验证 <see cref="PointsAppService.GetLedgerAsync"/> 真实分页查询与 DTO 映射。
/// 关联审计 PM-M07：原实现返回空列表，现改为调用仓储分页查询并映射为 PointsLedgerDto。
/// </summary>
public sealed class PointsAppServiceGetLedgerTests
{
    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<ICheckInRecordRepository> _checkInRepoMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly PointsAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    public PointsAppServiceGetLedgerTests()
    {
        _sut = new PointsAppService(
            _accountRepoMock.Object,
            _checkInRepoMock.Object,
            _memberRepoMock.Object,
            _uowMock.Object);
    }

    [Fact]
    public async Task GetLedgerAsync_Should_Map_Ledgers_To_Dtos()
    {
        // Arrange
        var occurredAt1 = DateTime.UtcNow.AddMinutes(-10);
        var occurredAt2 = DateTime.UtcNow;
        var ledgers = new List<PointsLedger>
        {
            PointsLedger.Create(
                Guid.NewGuid(), AccountId, PointsTxType.Earn, 50, 50,
                PointsSource.CheckIn, Guid.Empty, "签到返积分", occurredAt1),
            PointsLedger.Create(
                Guid.NewGuid(), AccountId, PointsTxType.Consume, 20, 30,
                PointsSource.Offset, Guid.NewGuid(), "订单消费", occurredAt2)
        };
        _accountRepoMock.Setup(r => r.GetLedgersByUserIdAsync(UserId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ledgers);

        // Act
        var result = await _sut.GetLedgerAsync(UserId, 1, 20);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        var first = result[0];
        first.AccountId.Should().Be(AccountId);
        first.TxType.Should().Be(PointsTxType.Earn);
        first.Amount.Should().Be(50);
        first.BalanceAfter.Should().Be(50);
        first.Source.Should().Be(PointsSource.CheckIn);
        first.Reason.Should().Be("签到返积分");
        first.OccurredAt.Should().Be(occurredAt1);

        var second = result[1];
        second.TxType.Should().Be(PointsTxType.Consume);
        second.Amount.Should().Be(20);
        second.BalanceAfter.Should().Be(30);
        second.Source.Should().Be(PointsSource.Offset);
        second.Reason.Should().Be("订单消费");
    }

    [Fact]
    public async Task GetLedgerAsync_Should_Pass_Pagination_To_Repository()
    {
        // Arrange
        _accountRepoMock.Setup(r => r.GetLedgersByUserIdAsync(UserId, 2, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PointsLedger>());

        // Act
        await _sut.GetLedgerAsync(UserId, 2, 15);

        // Assert：分页参数原样透传
        _accountRepoMock.Verify(
            r => r.GetLedgersByUserIdAsync(UserId, 2, 15, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLedgerAsync_Should_Clamp_Negative_Page_To_1()
    {
        // Arrange
        _accountRepoMock.Setup(r => r.GetLedgersByUserIdAsync(UserId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PointsLedger>());

        // Act：page=0 应被钳制为 1
        await _sut.GetLedgerAsync(UserId, 0, 20);

        _accountRepoMock.Verify(
            r => r.GetLedgersByUserIdAsync(UserId, 1, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLedgerAsync_Should_Clamp_Negative_PageSize_To_Default_20()
    {
        // Arrange
        _accountRepoMock.Setup(r => r.GetLedgersByUserIdAsync(UserId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PointsLedger>());

        // Act：pageSize=0 应被钳制为 20
        await _sut.GetLedgerAsync(UserId, 1, 0);

        _accountRepoMock.Verify(
            r => r.GetLedgersByUserIdAsync(UserId, 1, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLedgerAsync_Should_Clamp_PageSize_Over_100_To_100()
    {
        // Arrange
        _accountRepoMock.Setup(r => r.GetLedgersByUserIdAsync(UserId, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PointsLedger>());

        // Act：pageSize=500 应被钳制为 100
        await _sut.GetLedgerAsync(UserId, 1, 500);

        _accountRepoMock.Verify(
            r => r.GetLedgersByUserIdAsync(UserId, 1, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLedgerAsync_Should_Return_Empty_When_Repository_Returns_Empty()
    {
        // Arrange
        _accountRepoMock.Setup(r => r.GetLedgersByUserIdAsync(UserId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PointsLedger>());

        // Act
        var result = await _sut.GetLedgerAsync(UserId, 1, 20);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
