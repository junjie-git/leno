using Leno.ReviewAfterSales.Application;
using Leno.ReviewAfterSales.Application.InternalQueryServices;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Application;

/// <summary>
/// 审计 4.7：ReviewInternalQueryService.GetOrderReviewsAsync 返回 null 而非空集合。
/// 验证订单无可见评价时返回空 OrderReviewsDto（Reviews 为空列表），而非 null。
/// 实现层不再返回 null，签名保留 nullable 以兼容既有消费方与防御性编程。
/// </summary>
public sealed class ReviewInternalQueryServiceOrderReviewsTests
{
    [Fact]
    public async Task GetOrderReviewsAsync_Should_Return_Empty_Dto_When_No_Reviews()
    {
        var orderId = Guid.NewGuid();
        var repoMock = new Mock<IReviewRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(orderId, ReviewStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Review>());

        var svc = new ReviewInternalQueryService(repoMock.Object);

        var dto = await svc.GetOrderReviewsAsync(orderId);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.Reviews);
        Assert.Empty(dto.Reviews);
    }

    [Fact]
    public async Task GetOrderReviewsAsync_Should_Return_Dto_With_Reviews_When_Exists()
    {
        var orderId = Guid.NewGuid();
        var review = Review.Create(
            Guid.NewGuid(), orderId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), rating: 5, "good", null, sellerId: Guid.NewGuid());
        review.Approve(Guid.NewGuid());

        var repoMock = new Mock<IReviewRepository>();
        repoMock.Setup(r => r.GetByOrderIdAsync(orderId, ReviewStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Review> { review });

        var svc = new ReviewInternalQueryService(repoMock.Object);

        var dto = await svc.GetOrderReviewsAsync(orderId);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.Reviews);
        Assert.Single(dto.Reviews);
        Assert.Equal(review.Id, dto.Reviews.First().ReviewId);
        Assert.Equal(review.Rating, dto.Reviews.First().Rating);
    }
}
