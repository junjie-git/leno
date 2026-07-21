using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.ReviewAfterSales.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Infrastructure;

/// <summary>
/// 审计 3.3：整单售后（orderLineId 为 null）不做重复申请校验。
/// 验证整单售后重复申请时抛 AFTERSALES_DUPLICATE。
/// </summary>
public sealed class AfterSalesWholeOrderDuplicateTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    private readonly Mock<IOrderStatusProvider> _orderProviderMock = new();
    private readonly Mock<IAfterSalesRepository> _repoMock = new();
    private readonly AfterSalesEligibilityChecker _checker;

    public AfterSalesWholeOrderDuplicateTests()
    {
        _checker = new AfterSalesEligibilityChecker(
            _orderProviderMock.Object,
            _repoMock.Object,
            NullLogger<AfterSalesEligibilityChecker>.Instance);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_WholeOrder_Duplicate_Exists()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = 2,
                UserId = UserId,
                SellerId = SellerId,
                Items = new List<OrderItemStatusInfo>()
            });
        // 模拟同订单已存在进行中的整单售后
        _repoMock.Setup(r => r.HasActiveByOrderAsync(OrderId, AfterSalesType.RefundOnly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await _checker.EnsureEligibleAsync(
            OrderId, orderLineId: null, UserId, AfterSalesType.RefundOnly);

        var ex = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("AFTERSALES_DUPLICATE", ex.ErrorCode);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Pass_When_WholeOrder_NoDuplicate()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = 2,
                UserId = UserId,
                SellerId = SellerId,
                Items = new List<OrderItemStatusInfo>()
            });
        _repoMock.Setup(r => r.HasActiveByOrderAsync(OrderId, AfterSalesType.RefundOnly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var order = await _checker.EnsureEligibleAsync(
            OrderId, orderLineId: null, UserId, AfterSalesType.RefundOnly);

        Assert.Equal(OrderId, order.OrderId);
    }
}
