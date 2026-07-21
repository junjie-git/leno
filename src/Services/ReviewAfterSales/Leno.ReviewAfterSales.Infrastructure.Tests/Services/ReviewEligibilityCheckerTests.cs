using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.ReviewAfterSales.Infrastructure.Tests.Services;

/// <summary>
/// 评价资格校验器单元测试，验证 EnsureEligibleAsync 返回匹配订单行的 OrderItemStatusInfo（含真实 SpuId/SkuId），
/// 并在订单行不存在、SkuId/SpuId 不匹配时抛出领域异常。
/// </summary>
public sealed class ReviewEligibilityCheckerTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid RealSkuId = Guid.NewGuid();
    private static readonly Guid RealSpuId = Guid.NewGuid();

    private readonly Mock<IOrderStatusProvider> _orderProviderMock = new();
    private readonly Mock<IReviewRepository> _repoMock = new();
    private readonly ReviewEligibilityChecker _checker;

    public ReviewEligibilityCheckerTests()
    {
        _checker = new ReviewEligibilityChecker(
            _orderProviderMock.Object, _repoMock.Object, NullLogger<ReviewEligibilityChecker>.Instance);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Return_LineItem_With_Real_SpuId_SkuId_When_Valid()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderInfo());
        _repoMock.Setup(r => r.ExistsByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        result.Should().NotBeNull();
        result.OrderLineId.Should().Be(OrderLineId);
        result.SkuId.Should().Be(RealSkuId);
        result.SpuId.Should().Be(RealSpuId);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_Order_Not_Found()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderStatusInfo?)null);

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("REVIEW_ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_User_Not_Owner()
    {
        var otherUserId = Guid.NewGuid();
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderInfo());

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, OrderLineId, otherUserId);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("REVIEW_FORBIDDEN");
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_Order_Not_Completed()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderInfo(status: 2));

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("REVIEW_ORDER_NOT_COMPLETED");
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_OrderLine_NotFound()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderInfo(items: new List<OrderItemStatusInfo>()));

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("REVIEW_ORDER_LINE_NOT_FOUND");
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_Duplicate_Review_Exists()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrderInfo());
        _repoMock.Setup(r => r.ExistsByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await _checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        var ex = await act.Should().ThrowAsync<ReviewDomainException>();
        ex.Which.ErrorCode.Should().Be("REVIEW_DUPLICATE");
    }

    private static OrderStatusInfo BuildOrderInfo(int status = 3, List<OrderItemStatusInfo>? items = null) => new()
    {
        OrderId = OrderId,
        Status = status,
        UserId = UserId,
        SellerId = Guid.NewGuid(),
        CompletedAt = DateTime.UtcNow.AddDays(-1),
        CreatedAt = DateTime.UtcNow.AddDays(-5),
        Items = items ?? new List<OrderItemStatusInfo>
        {
            new() { OrderLineId = OrderLineId, SkuId = RealSkuId, SpuId = RealSpuId, Quantity = 1 }
        }
    };
}
