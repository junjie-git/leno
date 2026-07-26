using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Events;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.ValueObjects;

namespace Leno.ReviewAfterSales.Domain.Tests;

/// <summary>
/// 买家追评（AppendAdditionalReview）领域逻辑单元测试。
/// 验证：
/// - 已通过态且无追评时，追评成功并写入 AppendContent/AppendImages/AppendedAt，发布 ReviewAppendedDomainEvent
/// - 待审核/已隐藏态不可追评
/// - 已追评不可重复追评
/// - 内容非空、长度上限 500、图片最多 9 张校验
/// </summary>
public sealed class ReviewAppendAdditionalTests
{
    private static readonly Guid ReviewId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OrderLineId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SpuId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SkuId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid UserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid SellerId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid AuditorId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    #region Happy Path

    [Fact]
    public void AppendAdditionalReview_WhenApproved_And_NoExistingAppend_Should_SetAppendFields()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        review.AppendAdditionalReview("使用一段时间后依然很好", new List<string> { "append1.jpg" });

        // Assert
        review.AppendContent.Should().Be("使用一段时间后依然很好");
        review.AppendImages.Should().ContainSingle().Which.Should().Be("append1.jpg");
        review.AppendedAt.Should().NotBeNull();
        review.AppendedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AppendAdditionalReview_WhenApproved_And_NoExistingAppend_Should_RaiseReviewAppendedDomainEvent()
    {
        // Arrange
        var review = CreateApprovedReview();
        review.ClearDomainEvents();

        // Act
        review.AppendAdditionalReview("追评内容", new List<string> { "img.jpg" });

        // Assert
        review.DomainEvents.Should().HaveCount(1);
        var domainEvent = review.DomainEvents.Single().Should().BeOfType<ReviewAppendedDomainEvent>().Subject;
        domainEvent.ReviewId.Should().Be(ReviewId);
        domainEvent.UserId.Should().Be(UserId);
        domainEvent.SpuId.Should().Be(SpuId);
        domainEvent.Rating.Should().Be(4);
    }

    [Fact]
    public void AppendAdditionalReview_ContentAtMaximumBoundary_Should_SetAppendContent()
    {
        // Arrange
        var review = CreateApprovedReview();
        var content = new string('A', 500);

        // Act
        review.AppendAdditionalReview(content, new List<string>());

        // Assert
        review.AppendContent.Should().Be(content);
        review.AppendContent!.Length.Should().Be(500);
    }

    [Fact]
    public void AppendAdditionalReview_ImagesAtMaximumBoundary_Should_SetAppendImages()
    {
        // Arrange
        var review = CreateApprovedReview();
        var images = Enumerable.Range(1, 9).Select(i => $"append{i}.jpg").ToList();

        // Act
        review.AppendAdditionalReview("追评内容", images);

        // Assert
        review.AppendImages.Should().HaveCount(9);
    }

    [Fact]
    public void AppendAdditionalReview_NullImages_Should_SetEmptyAppendImages()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        review.AppendAdditionalReview("追评内容", null!);

        // Assert
        review.AppendImages.Should().NotBeNull();
        review.AppendImages.Should().BeEmpty();
    }

    [Fact]
    public void AppendAdditionalReview_EmptyImages_Should_SetEmptyAppendImages()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        review.AppendAdditionalReview("追评内容", new List<string>());

        // Assert
        review.AppendImages.Should().BeEmpty();
    }

    #endregion

    #region Validation Guards

    [Fact]
    public void AppendAdditionalReview_WhenPending_Should_ThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreatePendingReview();

        // Act
        var act = () => review.AppendAdditionalReview("追评内容", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_APPEND_STATUS_INVALID");
        review.AppendContent.Should().BeNull();
        review.AppendImages.Should().BeEmpty();
        review.AppendedAt.Should().BeNull();
    }

    [Fact]
    public void AppendAdditionalReview_WhenHidden_Should_ThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateHiddenReview();

        // Act
        var act = () => review.AppendAdditionalReview("追评内容", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_APPEND_STATUS_INVALID");
    }

    [Fact]
    public void AppendAdditionalReview_WhenAlreadyAppended_Should_ThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();
        review.AppendAdditionalReview("第一次追评", new List<string>());

        // Act
        var act = () => review.AppendAdditionalReview("第二次追评", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_ALREADY_APPENDED");
        review.AppendContent.Should().Be("第一次追评");
    }

    [Fact]
    public void AppendAdditionalReview_EmptyContent_Should_ThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        var act = () => review.AppendAdditionalReview("", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_APPEND_CONTENT_EMPTY");
    }

    [Fact]
    public void AppendAdditionalReview_WhitespaceContent_Should_ThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        var act = () => review.AppendAdditionalReview("   ", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_APPEND_CONTENT_EMPTY");
    }

    [Fact]
    public void AppendAdditionalReview_ContentTooLong_Should_ThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();
        var content = new string('B', 501);

        // Act
        var act = () => review.AppendAdditionalReview(content, new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_APPEND_CONTENT_TOO_LONG");
    }

    [Fact]
    public void AppendAdditionalReview_TooManyImages_Should_ThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();
        var images = Enumerable.Range(1, 10).Select(i => $"img{i}.jpg").ToList();

        // Act
        var act = () => review.AppendAdditionalReview("追评内容", images);

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_APPEND_IMAGES_TOO_MANY");
    }

    #endregion

    #region Helpers

    private static Review CreatePendingReview() =>
        Review.Create(ReviewId, OrderId, OrderLineId, SpuId, SkuId, UserId, 4,
            "A valid review content.", new List<string> { "img1.jpg" }, SellerId);

    private static Review CreateApprovedReview()
    {
        var review = CreatePendingReview();
        review.Approve(AuditorId);
        return review;
    }

    private static Review CreateHiddenReview()
    {
        var review = CreateApprovedReview();
        review.Hide(AuditorId, "Violation of policy");
        return review;
    }

    #endregion
}
