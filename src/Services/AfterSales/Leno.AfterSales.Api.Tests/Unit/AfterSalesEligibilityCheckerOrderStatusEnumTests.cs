using Leno.AfterSales.Domain.Exceptions;
using Leno.AfterSales.Domain.Repositories;
using Leno.AfterSales.Domain.Services;
using Leno.AfterSales.Domain.ValueObjects;
using Leno.AfterSales.Infrastructure.Services;
using Leno.SharedContracts.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.AfterSales.Api.Tests.Unit;

/// <summary>
/// 审计 3.9：订单状态硬编码（OrderStatusShipped=2 / OrderStatusCompleted=3），跨 BC 契约脆弱。
/// 验证 AfterSalesEligibilityChecker 使用共享枚举 <see cref="OrderStatusEnum"/> 替代魔法数，
/// 且枚举值与订单域对齐。
/// </summary>
public sealed class AfterSalesEligibilityCheckerOrderStatusEnumTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();

    private readonly Mock<IOrderStatusProvider> _orderProviderMock = new();
    private readonly Mock<IAfterSalesRepository> _afterSalesRepositoryMock = new();

    private AfterSalesEligibilityChecker CreateChecker()
        => new(
            _orderProviderMock.Object,
            _afterSalesRepositoryMock.Object,
            NullLogger<AfterSalesEligibilityChecker>.Instance);

    private static OrderStatusInfo BuildOrder(OrderStatusEnum status, DateTime? completedAt = null)
        => new()
        {
            OrderId = OrderId,
            Status = (int)status,
            UserId = UserId,
            SellerId = SellerId,
            CompletedAt = completedAt ?? default,
            Items = new List<OrderItemStatusInfo>()
        };

    [Fact]
    public async Task AfterSalesEligibilityChecker_Should_Accept_OrderStatus_Shipped_From_Enum()
    {
        var checker = CreateChecker();
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrder(OrderStatusEnum.Shipped));
        _afterSalesRepositoryMock.Setup(r => r.HasActiveByOrderLineAsync(It.IsAny<Guid>(), It.IsAny<AfterSalesType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var order = await checker.EnsureEligibleAsync(
            OrderId, OrderLineId, UserId, AfterSalesType.ReturnRefund);

        Assert.Equal((int)OrderStatusEnum.Shipped, order.Status);
    }

    [Fact]
    public async Task AfterSalesEligibilityChecker_Should_Accept_OrderStatus_Completed_From_Enum()
    {
        var checker = CreateChecker();
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrder(OrderStatusEnum.Completed, DateTime.UtcNow.AddDays(-1)));
        _afterSalesRepositoryMock.Setup(r => r.HasActiveByOrderLineAsync(It.IsAny<Guid>(), It.IsAny<AfterSalesType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var order = await checker.EnsureEligibleAsync(
            OrderId, OrderLineId, UserId, AfterSalesType.ReturnRefund);

        Assert.Equal((int)OrderStatusEnum.Completed, order.Status);
    }

    [Fact]
    public async Task AfterSalesEligibilityChecker_Should_Reject_OrderStatus_PendingPayment_From_Enum()
    {
        var checker = CreateChecker();
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrder(OrderStatusEnum.PendingPayment));

        var act = async () => await checker.EnsureEligibleAsync(
            OrderId, OrderLineId, UserId, AfterSalesType.ReturnRefund);

        var exception = await Assert.ThrowsAsync<AfterSalesDomainException>(act);
        Assert.Equal("AFTERSALES_STATUS_INVALID", exception.ErrorCode);
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
