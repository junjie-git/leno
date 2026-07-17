using Leno.Infrastructure.Abstractions;
using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using AfterSalesAggregate = Leno.ReviewAfterSales.Domain.Aggregates.AfterSales;

namespace Leno.ReviewAfterSales.Application.Tests;

/// <summary>
/// 售后应用服务单元测试，覆盖售后申请、审核驳回、撤销与仅退款入发件箱流程。
/// </summary>
public class AfterSalesAppServiceTests
{
    private readonly Mock<IAfterSalesRepository> _afterSalesRepoMock = new();
    private readonly Mock<IAfterSalesEligibilityChecker> _eligibilityMock = new();
    private readonly Mock<IPaymentInfoQueryService> _paymentInfoMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILogger<AfterSalesAppService>> _loggerMock = new();
    private readonly AfterSalesAppService _sut;

    private static readonly Guid AfterSalesId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid OperatorId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();

    public AfterSalesAppServiceTests()
    {
        _sut = new AfterSalesAppService(
            _afterSalesRepoMock.Object,
            _eligibilityMock.Object,
            _paymentInfoMock.Object,
            _eventBusMock.Object,
            _uowMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SubmitAfterSalesAsync_Valid_ShouldCreateAndSave()
    {
        var dto = BuildSubmitDto(AfterSalesType.ReturnRefund, 199m);

        var result = await _sut.SubmitAfterSalesAsync(UserId, dto);

        result.AfterSalesId.Should().NotBe(Guid.Empty);
        result.OrderId.Should().Be(OrderId);
        result.Status.Should().Be(AfterSalesStatus.Pending);
        result.RequestedAmount.Should().Be(199m);
        _eligibilityMock.Verify(e => e.EnsureEligibleAsync(OrderId, OrderLineId, UserId, AfterSalesType.ReturnRefund, It.IsAny<CancellationToken>()), Times.Once);
        _afterSalesRepoMock.Verify(r => r.AddAsync(It.IsAny<AfterSalesAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAfterSalesAsync_EligibilityCheckerThrows_ShouldPropagateAndNotSave()
    {
        _eligibilityMock
            .Setup(e => e.EnsureEligibleAsync(OrderId, OrderLineId, UserId, AfterSalesType.ReturnRefund, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("售后期已过"));
        var dto = BuildSubmitDto(AfterSalesType.ReturnRefund, 199m);

        var act = () => _sut.SubmitAfterSalesAsync(UserId, dto);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*售后期已过*");
        _afterSalesRepoMock.Verify(r => r.AddAsync(It.IsAny<AfterSalesAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectAfterSalesAsync_Pending_ShouldRejectAndSave()
    {
        var afterSales = CreatePendingAfterSales();
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        await _sut.RejectAfterSalesAsync(AfterSalesId, OperatorId, "不符合售后条件");

        afterSales.Status.Should().Be(AfterSalesStatus.Rejected);
        afterSales.RejectReason.Should().Be("不符合售后条件");
        afterSales.ApproverId.Should().Be(OperatorId);
        _afterSalesRepoMock.Verify(r => r.UpdateAsync(afterSales, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectAfterSalesAsync_NotFound_ShouldThrowInvalidOperationException()
    {
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AfterSalesAggregate?)null);

        var act = () => _sut.RejectAfterSalesAsync(AfterSalesId, OperatorId, "原因");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*售后单不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAfterSalesAsync_RefundOnly_ShouldMarkRefundingAndAddEvent()
    {
        var afterSales = CreatePendingRefundOnlyAfterSales();
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);
        _paymentInfoMock
            .Setup(p => p.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentInfoResult { PaymentId = PaymentId, Channel = "Alipay" });

        await _sut.ApproveAfterSalesAsync(AfterSalesId, SellerId, 50m);

        afterSales.Status.Should().Be(AfterSalesStatus.Refunding);
        afterSales.ApprovedAmount.Should().Be(50m);
        afterSales.DomainEvents.OfType<RefundRequestedIntegrationEvent>().Should().HaveCount(1);
        _paymentInfoMock.Verify(p => p.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _afterSalesRepoMock.Verify(r => r.UpdateAsync(afterSales, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAfterSalesAsync_ReturnRefund_ShouldNotMarkRefundingImmediately()
    {
        var afterSales = CreatePendingAfterSales();
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        await _sut.ApproveAfterSalesAsync(AfterSalesId, SellerId, 100m);

        afterSales.Status.Should().Be(AfterSalesStatus.Approved);
        afterSales.ApprovedAmount.Should().Be(100m);
        afterSales.DomainEvents.OfType<RefundRequestedIntegrationEvent>().Should().BeEmpty();
        _paymentInfoMock.Verify(p => p.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAfterSalesAsync_Pending_ShouldCancelAndSave()
    {
        var afterSales = CreatePendingAfterSales();
        _afterSalesRepoMock
            .Setup(r => r.GetByIdAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSales);

        await _sut.CancelAfterSalesAsync(AfterSalesId, UserId, "不需要了");

        afterSales.Status.Should().Be(AfterSalesStatus.Cancelled);
        afterSales.CancelReason.Should().Be("不需要了");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByOrderIdAsync_ShouldReturnDtoList()
    {
        var afterSalesList = new List<AfterSalesAggregate> { CreatePendingAfterSales() };
        _afterSalesRepoMock
            .Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSalesList);

        var result = await _sut.GetByOrderIdAsync(OrderId);

        result.Should().HaveCount(1);
        result[0].OrderId.Should().Be(OrderId);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnPaginatedResult()
    {
        var items = new List<AfterSalesAggregate> { CreatePendingAfterSales() };
        _afterSalesRepoMock
            .Setup(r => r.QueryAsync(null, null, null, AfterSalesStatus.Pending, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        _afterSalesRepoMock
            .Setup(r => r.CountAsync(null, null, null, AfterSalesStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.QueryAsync(null, null, null, AfterSalesStatus.Pending, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    private static SubmitAfterSalesDto BuildSubmitDto(AfterSalesType type, decimal amount) => new()
    {
        OrderId = OrderId,
        OrderLineId = OrderLineId,
        SellerId = SellerId,
        Type = type,
        ReasonCategory = "质量问题",
        Reason = "商品有破损",
        Images = [],
        RequestedAmount = amount,
        Currency = "CNY"
    };

    private static AfterSalesAggregate CreatePendingAfterSales() =>
        AfterSalesAggregate.Create(
            AfterSalesId, OrderId, OrderLineId, UserId, SellerId,
            AfterSalesType.ReturnRefund, "质量问题", "商品有破损", [], 199m, "CNY");

    private static AfterSalesAggregate CreatePendingRefundOnlyAfterSales() =>
        AfterSalesAggregate.Create(
            AfterSalesId, OrderId, OrderLineId, UserId, SellerId,
            AfterSalesType.RefundOnly, "质量问题", "商品有破损", [], 99m, "CNY");
}
