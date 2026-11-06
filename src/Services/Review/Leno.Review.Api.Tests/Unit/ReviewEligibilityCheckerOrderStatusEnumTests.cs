using Leno.Review.Domain.Exceptions;
using Leno.Review.Domain.Repositories;
using Leno.Review.Domain.Services;
using Leno.Review.Infrastructure.Services;
using Leno.SharedContracts.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Review.Api.Tests.Unit;

/// <summary>
/// 审计 3.9：订单状态硬编码（OrderStatusShipped=2 / OrderStatusCompleted=3），跨 BC 契约脆弱。
/// 验证 ReviewEligibilityChecker 使用共享枚举 <see cref="OrderStatusEnum"/> 替代魔法数。
/// </summary>
public sealed class ReviewEligibilityCheckerOrderStatusEnumTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();

    private readonly Mock<IOrderStatusProvider> _orderProviderMock = new();
    private readonly Mock<IReviewRepository> _reviewRepositoryMock = new();

    private ReviewEligibilityChecker CreateChecker()
        => new(
            _orderProviderMock.Object,
            _reviewRepositoryMock.Object,
            NullLogger<ReviewEligibilityChecker>.Instance);

    private static OrderStatusInfo BuildOrder(OrderStatusEnum status, DateTime? completedAt = null)
        => new()
        {
            OrderId = OrderId,
            Status = (int)status,
            UserId = UserId,
            SellerId = SellerId,
            CompletedAt = completedAt ?? default,
            Items = new List<OrderItemStatusInfo>
            {
                new()
                {
                    OrderLineId = OrderLineId,
                    SkuId = Guid.NewGuid(),
                    SpuId = Guid.NewGuid(),
                    Quantity = 1
                }
            }
        };

    [Fact]
    public async Task ReviewEligibilityChecker_Should_Accept_OrderStatus_Completed_From_Enum()
    {
        var checker = CreateChecker();
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrder(OrderStatusEnum.Completed, DateTime.UtcNow.AddDays(-1)));
        _reviewRepositoryMock.Setup(r => r.ExistsByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var lineItem = await checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        Assert.Equal(OrderLineId, lineItem.OrderLineId);
    }

    [Fact]
    public async Task ReviewEligibilityChecker_Should_Reject_OrderStatus_Shipped_From_Enum()
    {
        var checker = CreateChecker();
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrder(OrderStatusEnum.Shipped));

        var act = async () => await checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        var exception = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("REVIEW_ORDER_NOT_COMPLETED", exception.ErrorCode);
    }

    [Fact]
    public async Task ReviewEligibilityChecker_Should_Reject_Duplicate_Review()
    {
        var checker = CreateChecker();
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrder(OrderStatusEnum.Completed, DateTime.UtcNow.AddDays(-1)));
        _reviewRepositoryMock.Setup(r => r.ExistsByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await checker.EnsureEligibleAsync(OrderId, OrderLineId, UserId);

        var exception = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("REVIEW_DUPLICATE", exception.ErrorCode);
    }
}
