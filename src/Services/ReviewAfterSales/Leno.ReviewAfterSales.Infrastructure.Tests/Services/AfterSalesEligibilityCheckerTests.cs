using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.ReviewAfterSales.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.ReviewAfterSales.Infrastructure.Tests.Services;

/// <summary>
/// 售后资格校验器单元测试，验证 EnsureEligibleAsync 返回携带真实 SellerId 的 OrderStatusInfo，
/// 并在订单不存在、申请人非订单买家、订单状态不支持、超出售后期限时抛出领域异常。
/// </summary>
public sealed class AfterSalesEligibilityCheckerTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    private readonly Mock<IOrderStatusProvider> _orderProviderMock = new();
    private readonly Mock<IAfterSalesRepository> _repoMock = new();
    private readonly AfterSalesEligibilityChecker _checker;

    public AfterSalesEligibilityCheckerTests()
    {
        _checker = new AfterSalesEligibilityChecker(
            _orderProviderMock.Object,
            _repoMock.Object,
            NullLogger<AfterSalesEligibilityChecker>.Instance);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Return_Order_With_Real_SellerId_When_Valid()
    {
        var orderInfo = BuildOrderInfo(status: 2, sellerId: SellerId);
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderInfo);
        _repoMock.Setup(r => r.HasActiveByOrderLineAsync(It.IsAny<Guid>(), It.IsAny<AfterSalesType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _checker.EnsureEligibleAsync(OrderId, orderLineId: null, UserId, AfterSalesType.RefundOnly);

        result.Should().NotBeNull();
        result.SellerId.Should().Be(SellerId);
        result.OrderId.Should().Be(OrderId);
        result.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_Order_Not_Found()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderStatusInfo?)null);

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, orderLineId: null, UserId, AfterSalesType.RefundOnly);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("AFTERSALES_ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_User_Not_Owner()
    {
        var otherUserId = Guid.NewGuid();
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderInfo(status: 2, sellerId: SellerId));

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, orderLineId: null, otherUserId, AfterSalesType.RefundOnly);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("AFTERSALES_FORBIDDEN");
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_Order_Status_Invalid()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderInfo(status: 0, sellerId: SellerId));

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, orderLineId: null, UserId, AfterSalesType.RefundOnly);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("AFTERSALES_STATUS_INVALID");
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_Window_Expired()
    {
        var expiredCompletedAt = DateTime.UtcNow.AddDays(-20);
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderInfo(status: 3, sellerId: SellerId, completedAt: expiredCompletedAt));

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, orderLineId: null, UserId, AfterSalesType.RefundOnly);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("AFTERSALES_WINDOW_EXPIRED");
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_Duplicate_Active_Exists()
    {
        var orderLineId = Guid.NewGuid();
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderInfo(status: 2, sellerId: SellerId));
        _repoMock.Setup(r => r.HasActiveByOrderLineAsync(orderLineId, AfterSalesType.RefundOnly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, orderLineId, UserId, AfterSalesType.RefundOnly);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("AFTERSALES_DUPLICATE");
    }

    private static OrderStatusInfo BuildOrderInfo(int status, Guid sellerId, DateTime? completedAt = null) => new()
    {
        OrderId = OrderId,
        Status = status,
        UserId = UserId,
        SellerId = sellerId,
        CompletedAt = completedAt ?? default,
        CreatedAt = DateTime.UtcNow.AddDays(-5),
        Items = new List<OrderItemStatusInfo>()
    };
}
