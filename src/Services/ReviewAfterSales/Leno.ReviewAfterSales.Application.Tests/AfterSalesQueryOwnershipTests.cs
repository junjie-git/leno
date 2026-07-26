using Leno.Infrastructure.Abstractions;
using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AfterSalesAggregate = Leno.ReviewAfterSales.Domain.Aggregates.AfterSales;
using ReviewAggregate = Leno.ReviewAfterSales.Domain.Aggregates.Review;

namespace Leno.ReviewAfterSales.Application.Tests;

/// <summary>
/// P0-2.10 买家按订单/订单行查询归属校验单元测试。
/// 验证：
/// - 买家 A 越权查询买家 B 的订单售后单时抛 AFTERSALES_FORBIDDEN
/// - 买家 A 越权查询买家 B 的订单行评价时抛 REVIEW_FORBIDDEN
/// - 归属买家正常查询返回数据
/// - 评价不存在时返回 null（不触发订单域查询）
/// </summary>
public sealed class AfterSalesQueryOwnershipTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();
    private static readonly Guid ReviewId = Guid.NewGuid();

    [Fact]
    public async Task GetByOrderIdForUserAsync_Should_Throw_When_User_Not_Order_Owner()
    {
        var repoMock = new Mock<IAfterSalesRepository>();
        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();
        var paymentMock = new Mock<IPaymentInfoQueryService>();
        var eventBusMock = new Mock<IEventBus>();
        var uowMock = new Mock<IUnitOfWork>();
        var orderStatusProviderMock = new Mock<IOrderStatusProvider>();

        // 订单归属 OtherUserId，调用方是 UserId（攻击者），应被拒绝
        orderStatusProviderMock
            .Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo { OrderId = OrderId, UserId = OtherUserId, SellerId = SellerId });

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            orderStatusProviderMock.Object, eventBusMock.Object, uowMock.Object,
            NullLogger<AfterSalesAppService>.Instance);

        var act = () => svc.GetByOrderIdForUserAsync(OrderId, UserId, CancellationToken.None);

        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "AFTERSALES_FORBIDDEN");

        // 验证归属校验通过前不查询售后单仓储
        repoMock.Verify(r => r.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        orderStatusProviderMock.Verify(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByOrderIdForUserAsync_Should_Return_Items_When_User_Is_Order_Owner()
    {
        var repoMock = new Mock<IAfterSalesRepository>();
        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();
        var paymentMock = new Mock<IPaymentInfoQueryService>();
        var eventBusMock = new Mock<IEventBus>();
        var uowMock = new Mock<IUnitOfWork>();
        var orderStatusProviderMock = new Mock<IOrderStatusProvider>();

        orderStatusProviderMock
            .Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo { OrderId = OrderId, UserId = UserId, SellerId = SellerId });

        var afterSalesList = new List<AfterSalesAggregate> { CreatePendingAfterSales() };
        repoMock
            .Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterSalesList);

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            orderStatusProviderMock.Object, eventBusMock.Object, uowMock.Object,
            NullLogger<AfterSalesAppService>.Instance);

        var result = await svc.GetByOrderIdForUserAsync(OrderId, UserId, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].OrderId.Should().Be(OrderId);
        repoMock.Verify(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByOrderIdForUserAsync_Should_Throw_When_Order_Not_Found()
    {
        var repoMock = new Mock<IAfterSalesRepository>();
        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();
        var paymentMock = new Mock<IPaymentInfoQueryService>();
        var eventBusMock = new Mock<IEventBus>();
        var uowMock = new Mock<IUnitOfWork>();
        var orderStatusProviderMock = new Mock<IOrderStatusProvider>();

        // 订单域返回 null
        orderStatusProviderMock
            .Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderStatusInfo?)null);

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            orderStatusProviderMock.Object, eventBusMock.Object, uowMock.Object,
            NullLogger<AfterSalesAppService>.Instance);

        var act = () => svc.GetByOrderIdForUserAsync(OrderId, UserId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*订单不存在*OrderId={OrderId}*");

        repoMock.Verify(r => r.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetReviewByOrderLineForUserAsync_Should_Throw_When_User_Not_Order_Owner()
    {
        var reviewRepoMock = new Mock<IReviewRepository>();
        var eligibilityMock = new Mock<IReviewEligibilityChecker>();
        var orderStatusProviderMock = new Mock<IOrderStatusProvider>();
        var productInfoQueryServiceMock = new Mock<IProductInfoQueryService>();
        var uowMock = new Mock<IUnitOfWork>();

        // 评价存在且 OrderId 关联订单归属 OtherUserId，调用方 UserId 是攻击者
        reviewRepoMock
            .Setup(r => r.GetByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateApprovedReview());
        orderStatusProviderMock
            .Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo { OrderId = OrderId, UserId = OtherUserId, SellerId = SellerId });

        var svc = new ReviewAppService(
            reviewRepoMock.Object, eligibilityMock.Object, orderStatusProviderMock.Object,
            productInfoQueryServiceMock.Object, uowMock.Object, NullLogger<ReviewAppService>.Instance);

        var act = () => svc.GetReviewByOrderLineForUserAsync(OrderLineId, UserId, CancellationToken.None);

        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "REVIEW_FORBIDDEN");

        orderStatusProviderMock.Verify(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReviewByOrderLineForUserAsync_Should_Return_Dto_When_User_Is_Order_Owner()
    {
        var reviewRepoMock = new Mock<IReviewRepository>();
        var eligibilityMock = new Mock<IReviewEligibilityChecker>();
        var orderStatusProviderMock = new Mock<IOrderStatusProvider>();
        var productInfoQueryServiceMock = new Mock<IProductInfoQueryService>();
        var uowMock = new Mock<IUnitOfWork>();

        reviewRepoMock
            .Setup(r => r.GetByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateApprovedReview());
        orderStatusProviderMock
            .Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo { OrderId = OrderId, UserId = UserId, SellerId = SellerId });

        var svc = new ReviewAppService(
            reviewRepoMock.Object, eligibilityMock.Object, orderStatusProviderMock.Object,
            productInfoQueryServiceMock.Object, uowMock.Object, NullLogger<ReviewAppService>.Instance);

        var result = await svc.GetReviewByOrderLineForUserAsync(OrderLineId, UserId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ReviewId.Should().Be(ReviewId);
        result.OrderId.Should().Be(OrderId);
        orderStatusProviderMock.Verify(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReviewByOrderLineForUserAsync_Should_Return_Null_When_Review_Not_Exists()
    {
        var reviewRepoMock = new Mock<IReviewRepository>();
        var eligibilityMock = new Mock<IReviewEligibilityChecker>();
        var orderStatusProviderMock = new Mock<IOrderStatusProvider>();
        var productInfoQueryServiceMock = new Mock<IProductInfoQueryService>();
        var uowMock = new Mock<IUnitOfWork>();

        // 评价不存在，应直接返回 null，不调用订单域防腐层
        reviewRepoMock
            .Setup(r => r.GetByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewAggregate?)null);

        var svc = new ReviewAppService(
            reviewRepoMock.Object, eligibilityMock.Object, orderStatusProviderMock.Object,
            productInfoQueryServiceMock.Object, uowMock.Object, NullLogger<ReviewAppService>.Instance);

        var result = await svc.GetReviewByOrderLineForUserAsync(OrderLineId, UserId, CancellationToken.None);

        result.Should().BeNull();
        // 评价不存在时不应触发订单域查询，避免无谓的远程调用
        orderStatusProviderMock.Verify(p => p.GetOrderStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AfterSalesAggregate CreatePendingAfterSales() =>
        AfterSalesAggregate.Create(
            AfterSalesId, OrderId, OrderLineId, UserId, SellerId,
            AfterSalesType.ReturnRefund, "质量问题", "商品有破损", [], 199m, "CNY");

    private static ReviewAggregate CreateApprovedReview()
    {
        var review = ReviewAggregate.Create(
            ReviewId, OrderId, OrderLineId, SpuId, SkuId, UserId, 5, "评价内容", [], SellerId);
        review.Approve(Guid.NewGuid());
        return review;
    }
}
