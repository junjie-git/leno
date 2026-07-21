using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.ReviewAfterSales.Infrastructure.Services;
using Leno.SharedContracts.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Infrastructure;

/// <summary>
/// 审计 3.9：订单状态硬编码（OrderStatusShipped=2 / OrderStatusCompleted=3），跨 BC 契约脆弱。
/// 验证 AfterSalesEligibilityChecker / ReviewEligibilityChecker 使用共享枚举 OrderStatusEnum
/// 替代魔法数，且枚举值与订单域对齐。
/// </summary>
public sealed class OrderStatusEnumAlignmentTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();

    private readonly Mock<IOrderStatusProvider> _orderProviderMock = new();
    private readonly Mock<IAfterSalesRepository> _afterSalesRepoMock = new();
    private readonly Mock<IReviewRepository> _reviewRepoMock = new();

    [Fact]
    public async Task AfterSalesEligibleChecker_Should_Accept_OrderStatus_Shipped_From_Enum()
    {
        var checker = new AfterSalesEligibilityChecker(
            _orderProviderMock.Object, _afterSalesRepoMock.Object,
            NullLogger<AfterSalesEligibilityChecker>.Instance);

        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = (int)OrderStatusEnum.Shipped,
                UserId = UserId,
                SellerId = SellerId,
                Items = new List<OrderItemStatusInfo>()
            });
        _afterSalesRepoMock.Setup(r => r.HasActiveByOrderLineAsync(It.IsAny<Guid>(), It.IsAny<AfterSalesType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var order = await checker.EnsureEligibleAsync(
            OrderId, OrderLineId, UserId, AfterSalesType.ReturnRefund);

        Assert.Equal((int)OrderStatusEnum.Shipped, order.Status);
    }

    [Fact]
    public async Task AfterSalesEligibleChecker_Should_Accept_OrderStatus_Completed_From_Enum()
    {
        var checker = new AfterSalesEligibilityChecker(
            _orderProviderMock.Object, _afterSalesRepoMock.Object,
            NullLogger<AfterSalesEligibilityChecker>.Instance);

        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = (int)OrderStatusEnum.Completed,
                UserId = UserId,
                SellerId = SellerId,
                CompletedAt = DateTime.UtcNow.AddDays(-1),
                Items = new List<OrderItemStatusInfo>()
            });
        _afterSalesRepoMock.Setup(r => r.HasActiveByOrderLineAsync(It.IsAny<Guid>(), It.IsAny<AfterSalesType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var order = await checker.EnsureEligibleAsync(
            OrderId, OrderLineId, UserId, AfterSalesType.ReturnRefund);

        Assert.Equal((int)OrderStatusEnum.Completed, order.Status);
    }

    [Fact]
    public async Task AfterSalesEligibleChecker_Should_Reject_OrderStatus_PendingPayment_From_Enum()
    {
        var checker = new AfterSalesEligibilityChecker(
            _orderProviderMock.Object, _afterSalesRepoMock.Object,
            NullLogger<AfterSalesEligibilityChecker>.Instance);

        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = (int)OrderStatusEnum.PendingPayment,
                UserId = UserId,
                SellerId = SellerId,
                Items = new List<OrderItemStatusInfo>()
            });

        var act = async () => await checker.EnsureEligibleAsync(
            OrderId, OrderLineId, UserId, AfterSalesType.ReturnRefund);

        var ex = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("AFTERSALES_STATUS_INVALID", ex.ErrorCode);
    }

    [Fact]
    public async Task ReviewEligibleChecker_Should_Accept_OrderStatus_Completed_From_Enum()
    {
        var checker = new ReviewEligibilityChecker(
            _orderProviderMock.Object, _reviewRepoMock.Object,
            NullLogger<ReviewEligibilityChecker>.Instance);

        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = (int)OrderStatusEnum.Completed,
                UserId = UserId,
                SellerId = SellerId,
                CompletedAt = DateTime.UtcNow.AddDays(-1),
                Items = new List<OrderItemStatusInfo>
                {
                    new() { OrderLineId = OrderLineId, SkuId = Guid.NewGuid(), SpuId = Guid.NewGuid(), Quantity = 1 }
                }
            });
        _reviewRepoMock.Setup(r => r.ExistsByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var lineItem = await checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        Assert.Equal(OrderLineId, lineItem.OrderLineId);
    }

    [Fact]
    public async Task ReviewEligibleChecker_Should_Reject_OrderStatus_Shipped_From_Enum()
    {
        var checker = new ReviewEligibilityChecker(
            _orderProviderMock.Object, _reviewRepoMock.Object,
            NullLogger<ReviewEligibilityChecker>.Instance);

        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = (int)OrderStatusEnum.Shipped,
                UserId = UserId,
                SellerId = SellerId,
                Items = new List<OrderItemStatusInfo>
                {
                    new() { OrderLineId = OrderLineId, SkuId = Guid.NewGuid(), SpuId = Guid.NewGuid(), Quantity = 1 }
                }
            });

        var act = async () => await checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        var ex = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("REVIEW_ORDER_NOT_COMPLETED", ex.ErrorCode);
    }

    [Fact]
    public void OrderStatusEnum_Should_Align_With_Order_Domain_Values()
    {
        // 验证共享枚举值与订单域 OrderStatus 严格对齐
        // 值映射：PendingPayment=0, Paid=1, Shipped=2, Completed=3, Cancelled=4, Closed=5
        Assert.Equal(0, (int)OrderStatusEnum.PendingPayment);
        Assert.Equal(1, (int)OrderStatusEnum.Paid);
        Assert.Equal(2, (int)OrderStatusEnum.Shipped);
        Assert.Equal(3, (int)OrderStatusEnum.Completed);
        Assert.Equal(4, (int)OrderStatusEnum.Cancelled);
        Assert.Equal(5, (int)OrderStatusEnum.Closed);
    }
}
