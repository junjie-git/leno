using FluentAssertions;
using Grpc.Core;
using Leno.ReviewAfterSales.Api.GrpcServices;
using Leno.ReviewAfterSales.Application;
using Leno.SharedContracts.Grpc.Review.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.Infrastructure.Tests.Grpc;

public class ReviewGrpcServiceTests
{
    [Fact]
    public async Task GetOrderReviews_Success_ReturnsMappedReviews()
    {
        var queryMock = new Mock<IReviewInternalQueryService>();
        var orderId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var spuId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-3);

        queryMock.Setup(q => q.GetOrderReviewsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderReviewsDto
            {
                Reviews = new List<ReviewSummaryDto>
                {
                    new()
                    {
                        ReviewId = reviewId,
                        SpuId = spuId,
                        Rating = 5,
                        Content = "非常好",
                        CreatedAt = createdAt
                    }
                }
            });

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);

        var result = await svc.GetOrderReviews(
            new GetOrderReviewsRequest { OrderId = orderId.ToString() },
            new TestServerCallContext());

        result.Reviews.Should().HaveCount(1);
        var review = result.Reviews[0];
        review.ReviewId.Should().Be(reviewId.ToString());
        review.Rating.Should().Be(5);
        review.Content.Should().Be("非常好");
        review.CreatedAt.Should().Be(createdAt.ToString("O"));
    }

    [Fact]
    public async Task GetOrderReviews_NotFound_ThrowsRpcException()
    {
        var queryMock = new Mock<IReviewInternalQueryService>();
        queryMock.Setup(q => q.GetOrderReviewsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderReviewsDto?)null);

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);

        var act = async () => await svc.GetOrderReviews(
            new GetOrderReviewsRequest { OrderId = Guid.NewGuid().ToString() },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrderReviews_InvalidArgument_ThrowsRpcException()
    {
        var queryMock = new Mock<IReviewInternalQueryService>(MockBehavior.Strict);
        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);

        var act = async () => await svc.GetOrderReviews(
            new GetOrderReviewsRequest { OrderId = "not-a-guid" },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task GetProductRating_Success_ReturnsMappedRating()
    {
        var queryMock = new Mock<IReviewInternalQueryService>();
        // 与 ReviewGrpcService 的 int64→Guid 简化映射保持一致
        var spuId = new Guid(42, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        queryMock.Setup(q => q.GetProductRatingAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductRatingDto
            {
                SpuId = spuId,
                AverageRating = 4.5,
                TotalCount = 10,
                PositiveCount = 8
            });

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);

        var result = await svc.GetProductRating(
            new GetProductRatingRequest { SpuId = 42L },
            new TestServerCallContext());

        result.AverageRating.Should().Be(4.5);
        result.TotalCount.Should().Be(10);
        result.PositiveCount.Should().Be(8);
    }

    [Fact]
    public async Task GetProductRating_NotFound_ThrowsRpcException()
    {
        var queryMock = new Mock<IReviewInternalQueryService>();
        queryMock.Setup(q => q.GetProductRatingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductRatingDto?)null);

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);

        var act = async () => await svc.GetProductRating(
            new GetProductRatingRequest { SpuId = 42L },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.NotFound);
    }
}
