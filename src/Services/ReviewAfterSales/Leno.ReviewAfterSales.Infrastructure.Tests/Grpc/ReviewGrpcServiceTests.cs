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
        // 验证 Guid→string 迁移双写字段（新客户端优先读 string）
        review.SpuIdStr.Should().Be(spuId.ToString());
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

        // int64 SpuId 已废弃（不可逆），必须传 SpuIdStr（Guid 字符串）
        var result = await svc.GetProductRating(
            new GetProductRatingRequest { SpuIdStr = spuId.ToString() },
            new TestServerCallContext());

        result.AverageRating.Should().Be(4.5);
        result.TotalCount.Should().Be(10);
        result.PositiveCount.Should().Be(8);
        // 验证 Guid→string 迁移双写字段（新客户端优先读 string）
        result.SpuIdStr.Should().Be(spuId.ToString());
    }

    [Fact]
    public async Task GetProductRating_NewClient_UsesStringId_ParsesGuid()
    {
        // 新客户端：仅传 SpuIdStr（Guid.ToString()），不传 SpuId（int64）
        var queryMock = new Mock<IReviewInternalQueryService>();
        var spuId = Guid.NewGuid();

        queryMock.Setup(q => q.GetProductRatingAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductRatingDto
            {
                SpuId = spuId,
                AverageRating = 4.8,
                TotalCount = 20,
                PositiveCount = 18
            });

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);

        var result = await svc.GetProductRating(
            new GetProductRatingRequest { SpuIdStr = spuId.ToString() },
            new TestServerCallContext());

        result.SpuIdStr.Should().Be(spuId.ToString());
        result.AverageRating.Should().Be(4.8);
        // 验证 queryService 收到的 Guid 与 SpuIdStr 解析结果一致
        queryMock.Verify(q => q.GetProductRatingAsync(spuId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductRating_LegacyClient_OnlyInt64_ThrowsInvalidArgument()
    {
        // 旧客户端：仅传 SpuId（int64），不传 SpuIdStr。
        // 契约变更：int64→Guid 的 GetHashCode 映射不可逆，服务端已强制废弃该字段，
        // 仅传 int64 时应返回 InvalidArgument 提示客户端升级。
        var queryMock = new Mock<IReviewInternalQueryService>(MockBehavior.Strict);
        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);

        var act = async () => await svc.GetProductRating(
            new GetProductRatingRequest { SpuId = 42L },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>())
            .Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        queryMock.Verify(q => q.GetProductRatingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProductRating_InvalidStringId_ThrowsInvalidArgument()
    {
        // 新客户端传了无效 SpuIdStr，应返回 InvalidArgument
        var queryMock = new Mock<IReviewInternalQueryService>(MockBehavior.Strict);
        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);

        var act = async () => await svc.GetProductRating(
            new GetProductRatingRequest { SpuIdStr = "not-a-guid" },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task GetProductRating_NotFound_ThrowsRpcException()
    {
        var queryMock = new Mock<IReviewInternalQueryService>();
        var spuId = Guid.NewGuid();
        queryMock.Setup(q => q.GetProductRatingAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductRatingDto?)null);

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);

        var act = async () => await svc.GetProductRating(
            new GetProductRatingRequest { SpuIdStr = spuId.ToString() },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.NotFound);
    }
}
