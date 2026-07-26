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
using ReviewAggregate = Leno.ReviewAfterSales.Domain.Aggregates.Review;

namespace Leno.ReviewAfterSales.Application.Tests;

/// <summary>
/// 买家追评（AppendAdditionalReviewAsync）应用服务单元测试。
/// 验证：
/// - 成功追评：归属买家调用已通过评价追评，写入 AppendContent/AppendImages/AppendedAt，调用仓储更新与 UoW 保存
/// - 失败场景：评价不存在抛 InvalidOperationException
/// - 鉴权场景：非归属买家追评抛 REVIEW_FORBIDDEN
/// - 领域校验：待审核态/已隐藏态/已追评/内容空/内容超长/图片超限透传领域异常
/// </summary>
public sealed class ReviewAppendAppServiceTests
{
    private readonly Mock<IReviewRepository> _reviewRepoMock = new();
    private readonly Mock<IReviewEligibilityChecker> _eligibilityMock = new();
    private readonly Mock<IOrderStatusProvider> _orderStatusProviderMock = new();
    private readonly Mock<IProductInfoQueryService> _productInfoQueryServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly ReviewAppService _sut;

    private static readonly Guid ReviewId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid AuditorId = Guid.NewGuid();

    public ReviewAppendAppServiceTests()
    {
        _sut = new ReviewAppService(
            _reviewRepoMock.Object,
            _eligibilityMock.Object,
            _orderStatusProviderMock.Object,
            _productInfoQueryServiceMock.Object,
            _uowMock.Object,
            NullLogger<ReviewAppService>.Instance);
    }

    #region Happy Path

    [Fact]
    public async Task AppendAdditionalReviewAsync_OwnerUser_ApprovedReview_ShouldAppendAndSave()
    {
        // Arrange: 已通过评价，归属买家本人追评
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var dto = new AppendReviewDto
        {
            Content = "使用一段时间后依然很好",
            Images = new List<string> { "append1.jpg", "append2.jpg" }
        };

        // Act
        var result = await _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ReviewId.Should().Be(ReviewId);
        result.AppendContent.Should().Be("使用一段时间后依然很好");
        result.AppendImages.Should().HaveCount(2);
        result.AppendImages.Should().ContainInOrder("append1.jpg", "append2.jpg");
        result.AppendedAt.Should().NotBeNull();
        review.AppendContent.Should().Be("使用一段时间后依然很好");
        _reviewRepoMock.Verify(r => r.UpdateAsync(review, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AppendAdditionalReviewAsync_NullImages_ShouldSetEmptyAppendImages()
    {
        // Arrange
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var dto = new AppendReviewDto
        {
            Content = "追评内容",
            Images = null!
        };

        // Act
        var result = await _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        result.AppendImages.Should().BeEmpty();
        review.AppendImages.Should().BeEmpty();
    }

    [Fact]
    public async Task AppendAdditionalReviewAsync_ContentAtMaximumBoundary_ShouldSucceed()
    {
        // Arrange
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var content = new string('A', 500);
        var dto = new AppendReviewDto { Content = content, Images = new List<string>() };

        // Act
        var result = await _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        result.AppendContent.Should().Be(content);
        result.AppendContent!.Length.Should().Be(500);
    }

    [Fact]
    public async Task AppendAdditionalReviewAsync_ImagesAtMaximumBoundary_ShouldSucceed()
    {
        // Arrange
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var images = Enumerable.Range(1, 9).Select(i => $"append{i}.jpg").ToList();
        var dto = new AppendReviewDto { Content = "追评内容", Images = images };

        // Act
        var result = await _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        result.AppendImages.Should().HaveCount(9);
    }

    #endregion

    #region Failure Scenarios

    [Fact]
    public async Task AppendAdditionalReviewAsync_ReviewNotFound_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewAggregate?)null);

        var dto = new AppendReviewDto { Content = "追评内容", Images = new List<string>() };

        // Act
        var act = () => _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*评价不存在*ReviewId={ReviewId}*");
        _reviewRepoMock.Verify(r => r.UpdateAsync(It.IsAny<ReviewAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AppendAdditionalReviewAsync_NullDto_ShouldThrowArgumentNullException()
    {
        // Arrange
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        // Act
        var act = () => _sut.AppendAdditionalReviewAsync(ReviewId, UserId, null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
        _reviewRepoMock.Verify(r => r.UpdateAsync(It.IsAny<ReviewAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Authorization Scenarios

    [Fact]
    public async Task AppendAdditionalReviewAsync_NonOwnerUser_ShouldThrowReviewForbidden()
    {
        // Arrange: 评价归属 UserId，调用方 OtherUserId（攻击者）越权追评
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var dto = new AppendReviewDto { Content = "恶意追评", Images = new List<string>() };

        // Act
        var act = () => _sut.AppendAdditionalReviewAsync(ReviewId, OtherUserId, dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "REVIEW_FORBIDDEN");
        // 越权场景不应调用仓储更新与保存
        _reviewRepoMock.Verify(r => r.UpdateAsync(It.IsAny<ReviewAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // 原评价不应被修改
        review.AppendContent.Should().BeNull();
    }

    #endregion

    #region Domain Validation Propagation

    [Fact]
    public async Task AppendAdditionalReviewAsync_PendingReview_ShouldPropagateDomainException()
    {
        // Arrange: 待审核态评价不可追评
        var review = CreatePendingReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var dto = new AppendReviewDto { Content = "追评内容", Images = new List<string>() };

        // Act
        var act = () => _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "REVIEW_APPEND_STATUS_INVALID");
        _reviewRepoMock.Verify(r => r.UpdateAsync(It.IsAny<ReviewAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AppendAdditionalReviewAsync_HiddenReview_ShouldPropagateDomainException()
    {
        // Arrange: 已隐藏态评价不可追评
        var review = CreateHiddenReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var dto = new AppendReviewDto { Content = "追评内容", Images = new List<string>() };

        // Act
        var act = () => _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "REVIEW_APPEND_STATUS_INVALID");
    }

    [Fact]
    public async Task AppendAdditionalReviewAsync_AlreadyAppended_ShouldPropagateDomainException()
    {
        // Arrange: 已追评过的评价不可重复追评
        var review = CreateApprovedReview();
        review.AppendAdditionalReview("第一次追评", new List<string> { "first.jpg" });
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var dto = new AppendReviewDto { Content = "第二次追评", Images = new List<string>() };

        // Act
        var act = () => _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "REVIEW_ALREADY_APPENDED");
        // 原追评内容不应被覆盖
        review.AppendContent.Should().Be("第一次追评");
    }

    [Fact]
    public async Task AppendAdditionalReviewAsync_EmptyContent_ShouldPropagateDomainException()
    {
        // Arrange
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var dto = new AppendReviewDto { Content = "", Images = new List<string>() };

        // Act
        var act = () => _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "REVIEW_APPEND_CONTENT_EMPTY");
    }

    [Fact]
    public async Task AppendAdditionalReviewAsync_ContentTooLong_ShouldPropagateDomainException()
    {
        // Arrange
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var dto = new AppendReviewDto
        {
            Content = new string('B', 501),
            Images = new List<string>()
        };

        // Act
        var act = () => _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "REVIEW_APPEND_CONTENT_TOO_LONG");
    }

    [Fact]
    public async Task AppendAdditionalReviewAsync_TooManyImages_ShouldPropagateDomainException()
    {
        // Arrange
        var review = CreateApprovedReview();
        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var dto = new AppendReviewDto
        {
            Content = "追评内容",
            Images = Enumerable.Range(1, 10).Select(i => $"img{i}.jpg").ToList()
        };

        // Act
        var act = () => _sut.AppendAdditionalReviewAsync(ReviewId, UserId, dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ReviewDomainException>()
            .Where(ex => ex.ErrorCode == "REVIEW_APPEND_IMAGES_TOO_MANY");
    }

    #endregion

    #region Helpers

    private static ReviewAggregate CreatePendingReview() =>
        ReviewAggregate.Create(ReviewId, OrderId, OrderLineId, SpuId, SkuId, UserId, 5,
            "原始评价内容", new List<string> { "img1.jpg" }, SellerId);

    private static ReviewAggregate CreateApprovedReview()
    {
        var review = CreatePendingReview();
        review.Approve(AuditorId);
        return review;
    }

    private static ReviewAggregate CreateHiddenReview()
    {
        var review = CreateApprovedReview();
        review.Hide(AuditorId, "违规内容");
        return review;
    }

    #endregion
}
