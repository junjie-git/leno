using Leno.Infrastructure.Abstractions;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AfterSalesAggregate = Leno.ReviewAfterSales.Domain.Aggregates.AfterSales;

namespace Leno.ReviewAfterSales.Application.Tests;

/// <summary>
/// 卖家查询售后单详情（GetByIdForSellerAsync）应用服务单元测试（P0）。
/// 验证：
/// - 成功场景：归属卖家查询返回完整 DTO
/// - 失败场景：售后单不存在抛 InvalidOperationException
/// - 鉴权场景：sellerId 为 Guid.Empty 抛 OPERATOR_EMPTY
/// - 卖家隔离场景：非归属卖家抛 AFTERSALES_NOT_OWNED，且不返回任何数据
/// </summary>
public sealed class AfterSalesGetByIdForSellerTests
{
    private readonly Mock<IAfterSalesRepository> _afterSalesRepoMock = new();
    private readonly Mock<IAfterSalesEligibilityChecker> _eligibilityMock = new();
    private readonly Mock<IPaymentInfoQueryService> _paymentInfoMock = new();
    private readonly Mock<IOrderStatusProvider> _orderStatusProviderMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly AfterSalesAppService _sut;

    private static readonly Guid AfterSalesId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OwnerSellerId = Guid.NewGuid();
    private static readonly Guid OtherSellerId = Guid.NewGuid();

    public AfterSalesGetByIdForSellerTests()
    {
        _sut = new AfterSalesAppService(
            _afterSalesRepoMock.Object,
            _eligibilityMock.Object,
            _paymentInfoMock.Object,
            _orderStatusProviderMock.Object,
            _eventBusMock.Object,
            _uowMock.Object,
            NullLogger<AfterSalesAppService>.Instance);
    }

    #region Happy Path

    [Fact]
    public async Task GetByIdForSellerAsync_OwnerSeller_ShouldReturnDto()
    {
        // Arrange: 售后单归属 OwnerSellerId，调用方为归属卖家本人
        var afterSales = CreatePendingAfterSales(OwnerSellerId);
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        // Act
        var result = await _sut.GetByIdForSellerAsync(AfterSalesId, OwnerSellerId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AfterSalesId.Should().Be(AfterSalesId);
        result.OrderId.Should().Be(OrderId);
        result.SellerId.Should().Be(OwnerSellerId);
        result.Status.Should().Be(AfterSalesStatus.Pending);
        result.Type.Should().Be(AfterSalesType.ReturnRefund);
        result.RequestedAmount.Should().Be(199m);
        result.Currency.Should().Be("CNY");
        // 查询场景不应触发任何写操作
        _afterSalesRepoMock.Verify(r => r.UpdateAsync(It.IsAny<AfterSalesAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdForSellerAsync_OwnerSeller_RefundOnly_ShouldReturnDto()
    {
        // Arrange: 仅退款类型售后单
        var afterSales = AfterSalesAggregate.Create(
            AfterSalesId, OrderId, OrderLineId, UserId, OwnerSellerId,
            AfterSalesType.RefundOnly, "质量问题", "商品损坏", new List<string>(), 99m, "CNY");
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        // Act
        var result = await _sut.GetByIdForSellerAsync(AfterSalesId, OwnerSellerId, CancellationToken.None);

        // Assert
        result.Type.Should().Be(AfterSalesType.RefundOnly);
        result.RequestedAmount.Should().Be(99m);
    }

    [Fact]
    public async Task GetByIdForSellerAsync_OwnerSeller_ApprovedStatus_ShouldReturnDto()
    {
        // Arrange: 已审核通过的售后单
        var afterSales = CreatePendingAfterSales(OwnerSellerId);
        afterSales.Approve(OwnerSellerId, 150m);
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        // Act
        var result = await _sut.GetByIdForSellerAsync(AfterSalesId, OwnerSellerId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(AfterSalesStatus.Approved);
        result.ApprovedAmount.Should().Be(150m);
        result.ApproverId.Should().Be(OwnerSellerId);
        result.ApprovedAt.Should().NotBeNull();
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task GetByIdForSellerAsync_NotFound_ShouldThrowInvalidOperationException()
    {
        // Arrange: 售后单不存在
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AfterSalesAggregate?)null);

        // Act
        var act = () => _sut.GetByIdForSellerAsync(AfterSalesId, OwnerSellerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*售后单不存在*AfterSalesId={AfterSalesId}*");
        // 不应触发任何写操作
        _afterSalesRepoMock.Verify(r => r.UpdateAsync(It.IsAny<AfterSalesAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Authorization Scenarios

    [Fact]
    public async Task GetByIdForSellerAsync_EmptySellerId_ShouldThrowOperatorEmpty()
    {
        // Arrange: sellerId 为 Guid.Empty（异常输入，可能由 JWT 解析失败导致）
        var afterSales = CreatePendingAfterSales(OwnerSellerId);
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        // Act
        var act = () => _sut.GetByIdForSellerAsync(AfterSalesId, Guid.Empty, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "OPERATOR_EMPTY");
    }

    #endregion

    #region Seller Isolation Scenarios

    [Fact]
    public async Task GetByIdForSellerAsync_NonOwnerSeller_ShouldThrowAfterSalesNotOwned()
    {
        // Arrange: 售后单归属 OwnerSellerId，调用方 OtherSellerId（攻击者）越权查询
        var afterSales = CreatePendingAfterSales(OwnerSellerId);
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        // Act
        var act = () => _sut.GetByIdForSellerAsync(AfterSalesId, OtherSellerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "AFTERSALES_NOT_OWNED");
        // 越权场景不应触发任何写操作
        _afterSalesRepoMock.Verify(r => r.UpdateAsync(It.IsAny<AfterSalesAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdForSellerAsync_NonOwnerSeller_ShouldNotLeakAfterSalesData()
    {
        // Arrange: 售后单归属 OwnerSellerId，调用方 OtherSellerId
        // 验证：即使越权查询，售后单聚合状态也不被修改（只读查询不触发状态变更）
        var afterSales = CreatePendingAfterSales(OwnerSellerId);
        var originalStatus = afterSales.Status;
        var originalApprovedAmount = afterSales.ApprovedAmount;
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        // Act
        var act = () => _sut.GetByIdForSellerAsync(AfterSalesId, OtherSellerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>();
        // 聚合状态不应被修改（只读查询路径无副作用）
        afterSales.Status.Should().Be(originalStatus);
        afterSales.ApprovedAmount.Should().Be(originalApprovedAmount);
    }

    #endregion

    #region Helpers

    private static AfterSalesAggregate CreatePendingAfterSales(Guid sellerId) =>
        AfterSalesAggregate.Create(
            AfterSalesId, OrderId, OrderLineId, UserId, sellerId,
            AfterSalesType.ReturnRefund, "质量问题", "商品有破损", new List<string>(), 199m, "CNY");

    #endregion
}
