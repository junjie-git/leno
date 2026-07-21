using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using ReviewAggregate = Leno.ReviewAfterSales.Domain.Aggregates.Review;

namespace Leno.ReviewAfterSales.Application.Tests;

/// <summary>
/// 评价应用服务单元测试，覆盖评价提交、卖家回复、运营审核与查询用例。
/// </summary>
public class ReviewAppServiceTests
{
    private readonly Mock<IReviewRepository> _reviewRepoMock = new();
    private readonly Mock<IReviewEligibilityChecker> _eligibilityMock = new();
    private readonly Mock<IOrderStatusProvider> _orderStatusProviderMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILogger<ReviewAppService>> _loggerMock = new();
    private readonly ReviewAppService _sut;

    private static readonly Guid ReviewId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid AuditorId = Guid.NewGuid();

    public ReviewAppServiceTests()
    {
        _sut = new ReviewAppService(
            _reviewRepoMock.Object,
            _eligibilityMock.Object,
            _orderStatusProviderMock.Object,
            _uowMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SubmitReviewAsync_Valid_ShouldCreateReviewAndSave()
    {
        // P0-2.2: EnsureEligibleAsync 返回携带真实 SpuId/SkuId 的订单行概要，
        // 应用层使用订单域返回的商品标识创建评价，忽略 dto 中的 SpuId/SkuId。
        // P0-2.7: EnsureEligibleAsync 同时返回 SellerId，应用层透传给 Review.Create 用于卖家回复归属校验。
        _eligibilityMock
            .Setup(e => e.EnsureEligibleAsync(OrderId, OrderLineId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderItemStatusInfo { OrderLineId = OrderLineId, SkuId = SkuId, SpuId = SpuId, SellerId = SellerId, Quantity = 1 });

        var dto = BuildSubmitDto();

        var result = await _sut.SubmitReviewAsync(UserId, dto);

        result.ReviewId.Should().NotBe(Guid.Empty);
        result.OrderId.Should().Be(OrderId);
        result.SpuId.Should().Be(SpuId);
        result.SkuId.Should().Be(SkuId);
        result.Rating.Should().Be(5);
        result.Status.Should().Be(ReviewStatus.Pending);
        _eligibilityMock.Verify(e => e.EnsureEligibleAsync(OrderId, OrderLineId, UserId, It.IsAny<CancellationToken>()), Times.Once);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<ReviewAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitReviewAsync_EligibilityCheckerThrows_ShouldPropagateAndNotSave()
    {
        _eligibilityMock
            .Setup(e => e.EnsureEligibleAsync(OrderId, OrderLineId, UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("订单未完成"));
        var dto = BuildSubmitDto();

        var act = () => _sut.SubmitReviewAsync(UserId, dto);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*订单未完成*");
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<ReviewAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SellerReplyAsync_ExistingReview_ShouldUpdateAndSave()
    {
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        await _sut.SellerReplyAsync(ReviewId, SellerId, "感谢评价，我们会持续改进");

        review.SellerReplyContent.Should().Be("感谢评价，我们会持续改进");
        _reviewRepoMock.Verify(r => r.UpdateAsync(review, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SellerReplyAsync_NotFound_ShouldThrowInvalidOperationException()
    {
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewAggregate?)null);

        var act = () => _sut.SellerReplyAsync(ReviewId, SellerId, "回复内容");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*评价不存在*");
        _reviewRepoMock.Verify(r => r.UpdateAsync(It.IsAny<ReviewAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveReviewAsync_PendingReview_ShouldApproveAndSave()
    {
        var review = CreatePendingReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        await _sut.ApproveReviewAsync(ReviewId, AuditorId);

        review.Status.Should().Be(ReviewStatus.Approved);
        review.AuditorId.Should().Be(AuditorId);
        review.AuditedAt.Should().NotBeNull();
        _reviewRepoMock.Verify(r => r.UpdateAsync(review, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveReviewAsync_NotFound_ShouldThrowInvalidOperationException()
    {
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewAggregate?)null);

        var act = () => _sut.ApproveReviewAsync(ReviewId, AuditorId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*评价不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HideReviewAsync_ApprovedReview_ShouldHideAndSave()
    {
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        await _sut.HideReviewAsync(ReviewId, AuditorId, "违规内容");

        review.Status.Should().Be(ReviewStatus.Hidden);
        review.HiddenBy.Should().Be(AuditorId);
        review.HideReason.Should().Be("违规内容");
        _reviewRepoMock.Verify(r => r.UpdateAsync(review, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReviewByOrderLineAsync_Existing_ShouldReturnDto()
    {
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var result = await _sut.GetReviewByOrderLineAsync(OrderLineId);

        result.Should().NotBeNull();
        result!.ReviewId.Should().Be(ReviewId);
        result.Rating.Should().Be(5);
    }

    [Fact]
    public async Task GetReviewByOrderLineAsync_NotExisting_ShouldReturnNull()
    {
        _reviewRepoMock
            .Setup(r => r.GetByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewAggregate?)null);

        var result = await _sut.GetReviewByOrderLineAsync(OrderLineId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task QueryReviewsAsync_ShouldReturnPaginatedResult()
    {
        var reviews = new List<ReviewAggregate> { CreateApprovedReview() };
        _reviewRepoMock
            .Setup(r => r.QueryAsync(null, null, ReviewStatus.Pending, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);
        _reviewRepoMock
            .Setup(r => r.CountAsync(null, null, ReviewStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.QueryReviewsAsync(ReviewStatus.Pending, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetReviewsBySpuAsync_ShouldQueryApprovedOnly()
    {
        var reviews = new List<ReviewAggregate> { CreateApprovedReview() };
        _reviewRepoMock
            .Setup(r => r.QueryAsync(SpuId, null, ReviewStatus.Approved, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);
        _reviewRepoMock
            .Setup(r => r.CountAsync(SpuId, null, ReviewStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.GetReviewsBySpuAsync(SpuId, 1, 10);

        result.Items.Should().HaveCount(1);
        _reviewRepoMock.Verify(r => r.QueryAsync(SpuId, null, ReviewStatus.Approved, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SubmitReviewDto BuildSubmitDto() => new()
    {
        OrderId = OrderId,
        OrderLineId = OrderLineId,
        SpuId = SpuId,
        SkuId = SkuId,
        Rating = 5,
        Content = "商品质量很好，物流速度快",
        Images = []
    };

    private static ReviewAggregate CreatePendingReview() =>
        ReviewAggregate.Create(ReviewId, OrderId, OrderLineId, SpuId, SkuId, UserId, 5, "内容很好", [], SellerId);

    private static ReviewAggregate CreateApprovedReview()
    {
        var review = CreatePendingReview();
        review.Approve(AuditorId);
        return review;
    }
}
