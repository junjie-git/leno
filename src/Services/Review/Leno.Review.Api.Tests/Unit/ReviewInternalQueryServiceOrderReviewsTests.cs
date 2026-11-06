using Leno.Review.Application.InternalQueryServices;
using Leno.Review.Domain.Repositories;
using Leno.Review.Domain.ValueObjects;
using Moq;
using Xunit;

using ReviewAggregate = Leno.Review.Domain.Aggregates.Review;

namespace Leno.Review.Api.Tests.Unit;

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
        var repositoryMock = new Mock<IReviewRepository>();
        repositoryMock.Setup(r => r.GetByOrderIdAsync(orderId, ReviewStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReviewAggregate>());

        var service = new ReviewInternalQueryService(repositoryMock.Object);

        var dto = await service.GetOrderReviewsAsync(orderId);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.Reviews);
        Assert.Empty(dto.Reviews);
    }

    [Fact]
    public async Task GetOrderReviewsAsync_Should_Return_Dto_With_Reviews_When_Exists()
    {
        var orderId = Guid.NewGuid();
        var review = ReviewAggregate.Create(
            Guid.NewGuid(), orderId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), rating: 5, "good", new List<string>(), sellerId: Guid.NewGuid());
        review.Approve(Guid.NewGuid());

        var repositoryMock = new Mock<IReviewRepository>();
        repositoryMock.Setup(r => r.GetByOrderIdAsync(orderId, ReviewStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReviewAggregate> { review });

        var service = new ReviewInternalQueryService(repositoryMock.Object);

        var dto = await service.GetOrderReviewsAsync(orderId);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.Reviews);
        Assert.Single(dto.Reviews);
        Assert.Equal(review.Id, dto.Reviews[0].ReviewId);
        Assert.Equal(review.Rating, dto.Reviews[0].Rating);
    }
}
