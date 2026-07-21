using Grpc.Core;
using Leno.ReviewAfterSales.Api.GrpcServices;
using Leno.ReviewAfterSales.Application;
using Leno.SharedContracts.Grpc.Review.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.ReviewAfterSales.Api.Tests.GrpcServices;

/// <summary>
/// ReviewGrpcService Guid→long 映射单元测试（审计 2.5）。
/// 验证 MapToProto 不再使用 Guid.GetHashCode 不可逆映射，旧 int64 字段强制返回 0，
/// 新客户端必须读 SpuIdStr；请求路径拒绝非零 SpuId 旧客户端。
/// </summary>
public sealed class ReviewGrpcServiceMappingTests
{
    [Fact]
    public async Task GetProductRating_Should_Return_SpuIdStr_Matching_Input_And_Zero_Deprecated_Long()
    {
        var spuId = Guid.NewGuid();
        var queryMock = new Mock<IReviewInternalQueryService>();
        queryMock.Setup(q => q.GetProductRatingAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductRatingDto
            {
                SpuId = spuId,
                AverageRating = 4.5,
                TotalCount = 10,
                PositiveCount = 8
            });

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);
        var request = new GetProductRatingRequest { SpuIdStr = spuId.ToString() };

        var response = await svc.GetProductRating(request, new TestServerCallContext());

        response.SpuIdStr.Should().Be(spuId.ToString());
        response.SpuId.Should().Be(0);
        response.SpuId.Should().NotBe((long)spuId.GetHashCode());
        response.AverageRating.Should().Be(4.5);
        response.TotalCount.Should().Be(10);
        response.PositiveCount.Should().Be(8);
    }

    [Fact]
    public async Task GetProductRating_Should_Reject_Deprecated_NonZero_SpuId_Long()
    {
        var queryMock = new Mock<IReviewInternalQueryService>();
        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);
        var request = new GetProductRatingRequest { SpuId = 123L, SpuIdStr = string.Empty };

        var act = async () => await svc.GetProductRating(request, new TestServerCallContext());

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        queryMock.Verify(
            q => q.GetProductRatingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProductRating_Should_Reject_When_Both_SpuId_And_SpuIdStr_Empty()
    {
        var queryMock = new Mock<IReviewInternalQueryService>();
        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);
        var request = new GetProductRatingRequest { SpuId = 0L, SpuIdStr = string.Empty };

        var act = async () => await svc.GetProductRating(request, new TestServerCallContext());

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        queryMock.Verify(
            q => q.GetProductRatingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrderReviews_Should_Return_Reviews_With_Zero_Deprecated_SpuId_Long()
    {
        var spuId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var queryMock = new Mock<IReviewInternalQueryService>();
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
                        Content = "good",
                        CreatedAt = DateTime.UtcNow
                    }
                }
            });

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);
        var request = new GetOrderReviewsRequest { OrderId = orderId.ToString() };

        var response = await svc.GetOrderReviews(request, new TestServerCallContext());

        response.Reviews.Should().HaveCount(1);
        var summary = response.Reviews[0];
        summary.ReviewId.Should().Be(reviewId.ToString());
        summary.SpuIdStr.Should().Be(spuId.ToString());
        summary.SpuId.Should().Be(0);
        summary.SpuId.Should().NotBe((long)spuId.GetHashCode());
        summary.Rating.Should().Be(5);
        summary.Content.Should().Be("good");
    }
}
