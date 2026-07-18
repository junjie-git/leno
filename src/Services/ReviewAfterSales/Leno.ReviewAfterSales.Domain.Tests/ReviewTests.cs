using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Events;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.ReviewAfterSales.Domain.Tests;

public class ReviewTests
{
    private static readonly Guid ValidReviewId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ValidOrderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValidOrderLineId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ValidSpuId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ValidSkuId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ValidUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ValidAuditorId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ValidOperatorId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    #region Create - Happy Path

    [Fact]
    public void Create_AllValidParameters_ShouldCreateReviewWithCorrectProperties()
    {
        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            5, "Great product!", new List<string> { "img1.jpg", "img2.jpg" });

        // Assert
        review.Id.Should().Be(ValidReviewId);
        review.OrderId.Should().Be(ValidOrderId);
        review.OrderLineId.Should().Be(ValidOrderLineId);
        review.SpuId.Should().Be(ValidSpuId);
        review.SkuId.Should().Be(ValidSkuId);
        review.UserId.Should().Be(ValidUserId);
        review.Rating.Should().Be(5);
        review.Content.Should().Be("Great product!");
        review.Images.Should().HaveCount(2);
        review.Images.Should().Contain("img1.jpg");
        review.Images.Should().Contain("img2.jpg");
        review.Status.Should().Be(ReviewStatus.Pending);
        review.SubmittedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        review.SellerReplyContent.Should().BeNull();
        review.AuditedAt.Should().BeNull();
        review.AuditorId.Should().BeNull();
        review.HiddenAt.Should().BeNull();
        review.HiddenBy.Should().BeNull();
        review.HideReason.Should().BeNull();
    }

    [Fact]
    public void Create_ValidParameters_ShouldRaiseReviewSubmittedDomainEvent()
    {
        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            3, "Decent product.", new List<string>());

        // Assert
        review.DomainEvents.Should().HaveCount(1);
        var domainEvent = review.DomainEvents.Single().Should().BeOfType<ReviewSubmittedDomainEvent>().Subject;
        domainEvent.ReviewId.Should().Be(ValidReviewId);
        domainEvent.UserId.Should().Be(ValidUserId);
        domainEvent.SpuId.Should().Be(ValidSpuId);
        domainEvent.Rating.Should().Be(3);
    }

    [Fact]
    public void Create_WithScoreParameters_ShouldRaiseReviewSubmittedDomainEventWithScoreData()
    {
        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            5, "Excellent!", new List<string>(),
            newScore: 4.5, reviewCount: 10);

        // Assert
        review.DomainEvents.Should().HaveCount(1);
        var domainEvent = review.DomainEvents.Single().Should().BeOfType<ReviewSubmittedDomainEvent>().Subject;
        domainEvent.Rating.Should().Be(5);
        domainEvent.NewScore.Should().Be(4.5);
        domainEvent.ReviewCount.Should().Be(10);
    }

    [Fact]
    public void Create_DefaultScoreParameters_ShouldBeZero()
    {
        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            3, "Decent product.", new List<string>());

        // Assert
        var domainEvent = review.DomainEvents.OfType<ReviewSubmittedDomainEvent>().Single();
        domainEvent.NewScore.Should().Be(0);
        domainEvent.ReviewCount.Should().Be(0);
    }

    [Fact]
    public void Create_RatingAtMinimumBoundary_ShouldCreateSuccessfully()
    {
        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            1, "Minimum rating.", new List<string>());

        // Assert
        review.Rating.Should().Be(1);
    }

    [Fact]
    public void Create_RatingAtMaximumBoundary_ShouldCreateSuccessfully()
    {
        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            5, "Maximum rating.", new List<string>());

        // Assert
        review.Rating.Should().Be(5);
    }

    [Fact]
    public void Create_ContentAtMaximumBoundary_ShouldCreateSuccessfully()
    {
        // Arrange
        var content = new string('A', 500);

        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, content, new List<string>());

        // Assert
        review.Content.Should().Be(content);
        review.Content.Length.Should().Be(500);
    }

    [Fact]
    public void Create_ImagesAtMaximumBoundary_ShouldCreateSuccessfully()
    {
        // Arrange
        var images = Enumerable.Range(1, 9).Select(i => $"img{i}.jpg").ToList();

        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "Valid content.", images);

        // Assert
        review.Images.Should().HaveCount(9);
    }

    [Fact]
    public void Create_NullImages_ShouldCreateWithEmptyImageList()
    {
        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "Valid content.", null!);

        // Assert
        review.Images.Should().NotBeNull();
        review.Images.Should().BeEmpty();
    }

    [Fact]
    public void Create_EmptyImages_ShouldCreateWithEmptyImageList()
    {
        // Act
        var review = Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "Valid content.", new List<string>());

        // Assert
        review.Images.Should().BeEmpty();
    }

    #endregion

    #region Create - Validation Guards

    [Fact]
    public void Create_EmptyReviewId_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            Guid.Empty, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "Valid content.", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_ID_EMPTY");
    }

    [Fact]
    public void Create_EmptyOrderId_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, Guid.Empty, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "Valid content.", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_ORDER_EMPTY");
    }

    [Fact]
    public void Create_EmptyOrderLineId_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, Guid.Empty,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "Valid content.", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_ORDER_LINE_EMPTY");
    }

    [Fact]
    public void Create_EmptySpuId_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            Guid.Empty, ValidSkuId, ValidUserId,
            4, "Valid content.", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_SPU_EMPTY");
    }

    [Fact]
    public void Create_EmptySkuId_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, Guid.Empty, ValidUserId,
            4, "Valid content.", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_SKU_EMPTY");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, Guid.Empty,
            4, "Valid content.", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_USER_EMPTY");
    }

    [Fact]
    public void Create_RatingZero_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            0, "Valid content.", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_RATING_INVALID");
    }

    [Fact]
    public void Create_RatingGreaterThanFive_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            6, "Valid content.", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_RATING_INVALID");
    }

    [Fact]
    public void Create_RatingNegative_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            -1, "Valid content.", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_RATING_INVALID");
    }

    [Fact]
    public void Create_EmptyContent_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_CONTENT_EMPTY");
    }

    [Fact]
    public void Create_WhitespaceContent_ShouldThrowWithCorrectErrorCode()
    {
        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "   ", new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_CONTENT_EMPTY");
    }

    [Fact]
    public void Create_ContentTooLong_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var content = new string('B', 501);

        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, content, new List<string>());

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_CONTENT_TOO_LONG");
    }

    [Fact]
    public void Create_ImagesTooMany_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var images = Enumerable.Range(1, 10).Select(i => $"img{i}.jpg").ToList();

        // Act
        var act = () => Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "Valid content.", images);

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_IMAGES_TOO_MANY");
    }

    #endregion

    #region SellerReply - Happy Path

    [Fact]
    public void SellerReply_WhenApproved_ShouldSetReplyContent()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        review.SellerReply("Thank you for your review!");

        // Assert
        review.SellerReplyContent.Should().Be("Thank you for your review!");
    }

    [Fact]
    public void SellerReply_ContentAtMaximumBoundary_ShouldSetReplyContent()
    {
        // Arrange
        var review = CreateApprovedReview();
        var reply = new string('C', 500);

        // Act
        review.SellerReply(reply);

        // Assert
        review.SellerReplyContent.Should().Be(reply);
        review.SellerReplyContent!.Length.Should().Be(500);
    }

    #endregion

    #region SellerReply - Validation Guards

    [Fact]
    public void SellerReply_WhenPending_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreatePendingReview();

        // Act
        var act = () => review.SellerReply("Some reply");

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_REPLY_STATUS_INVALID");
    }

    [Fact]
    public void SellerReply_WhenHidden_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateHiddenReview();

        // Act
        var act = () => review.SellerReply("Some reply");

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_REPLY_STATUS_INVALID");
    }

    [Fact]
    public void SellerReply_EmptyContent_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        var act = () => review.SellerReply("");

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_REPLY_EMPTY");
    }

    [Fact]
    public void SellerReply_WhitespaceContent_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        var act = () => review.SellerReply("   ");

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_REPLY_EMPTY");
    }

    [Fact]
    public void SellerReply_ContentTooLong_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();
        var reply = new string('D', 501);

        // Act
        var act = () => review.SellerReply(reply);

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_REPLY_TOO_LONG");
    }

    #endregion

    #region Approve - Happy Path

    [Fact]
    public void Approve_WhenPending_ShouldSetApprovedProperties()
    {
        // Arrange
        var review = CreatePendingReview();

        // Act
        review.Approve(ValidAuditorId);

        // Assert
        review.Status.Should().Be(ReviewStatus.Approved);
        review.AuditorId.Should().Be(ValidAuditorId);
        review.AuditedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Approve_WhenPending_ShouldRaiseReviewApprovedDomainEvent()
    {
        // Arrange
        var review = CreatePendingReview();
        review.ClearDomainEvents();

        // Act
        review.Approve(ValidAuditorId);

        // Assert
        review.DomainEvents.Should().HaveCount(1);
        var domainEvent = review.DomainEvents.Single().Should().BeOfType<ReviewApprovedDomainEvent>().Subject;
        domainEvent.ReviewId.Should().Be(review.Id);
        domainEvent.UserId.Should().Be(ValidUserId);
        domainEvent.SpuId.Should().Be(ValidSpuId);
        domainEvent.Rating.Should().Be(4);
    }

    #endregion

    #region Approve - Validation Guards

    [Fact]
    public void Approve_WhenAlreadyApproved_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        var act = () => review.Approve(ValidAuditorId);

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_APPROVE_STATUS_INVALID");
    }

    [Fact]
    public void Approve_WhenHidden_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateHiddenReview();

        // Act
        var act = () => review.Approve(ValidAuditorId);

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_APPROVE_STATUS_INVALID");
    }

    [Fact]
    public void Approve_EmptyAuditorId_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreatePendingReview();

        // Act
        var act = () => review.Approve(Guid.Empty);

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_AUDITOR_EMPTY");
    }

    #endregion

    #region Hide - Happy Path

    [Fact]
    public void Hide_WhenApproved_ShouldSetHiddenProperties()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        review.Hide(ValidOperatorId, "Inappropriate content");

        // Assert
        review.Status.Should().Be(ReviewStatus.Hidden);
        review.HiddenBy.Should().Be(ValidOperatorId);
        review.HiddenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        review.HideReason.Should().Be("Inappropriate content");
    }

    [Fact]
    public void Hide_WhenApproved_ShouldRaiseReviewHiddenDomainEvent()
    {
        // Arrange
        var review = CreateApprovedReview();
        review.ClearDomainEvents();

        // Act
        review.Hide(ValidOperatorId, "Inappropriate content");

        // Assert
        review.DomainEvents.Should().HaveCount(1);
        var domainEvent = review.DomainEvents.Single().Should().BeOfType<ReviewHiddenDomainEvent>().Subject;
        domainEvent.ReviewId.Should().Be(review.Id);
        domainEvent.SpuId.Should().Be(ValidSpuId);
        domainEvent.Rating.Should().Be(4);
    }

    [Fact]
    public void Hide_ReasonAtMaximumBoundary_ShouldSetHideReason()
    {
        // Arrange
        var review = CreateApprovedReview();
        var reason = new string('E', 200);

        // Act
        review.Hide(ValidOperatorId, reason);

        // Assert
        review.HideReason.Should().Be(reason);
        review.HideReason!.Length.Should().Be(200);
    }

    #endregion

    #region Hide - Validation Guards

    [Fact]
    public void Hide_WhenPending_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreatePendingReview();

        // Act
        var act = () => review.Hide(ValidOperatorId, "Some reason");

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_HIDE_STATUS_INVALID");
    }

    [Fact]
    public void Hide_WhenAlreadyHidden_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateHiddenReview();

        // Act
        var act = () => review.Hide(ValidOperatorId, "Some reason");

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_HIDE_STATUS_INVALID");
    }

    [Fact]
    public void Hide_EmptyOperatorId_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        var act = () => review.Hide(Guid.Empty, "Some reason");

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_OPERATOR_EMPTY");
    }

    [Fact]
    public void Hide_EmptyReason_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        var act = () => review.Hide(ValidOperatorId, "");

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_HIDE_REASON_EMPTY");
    }

    [Fact]
    public void Hide_WhitespaceReason_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();

        // Act
        var act = () => review.Hide(ValidOperatorId, "   ");

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_HIDE_REASON_EMPTY");
    }

    [Fact]
    public void Hide_ReasonTooLong_ShouldThrowWithCorrectErrorCode()
    {
        // Arrange
        var review = CreateApprovedReview();
        var reason = new string('F', 201);

        // Act
        var act = () => review.Hide(ValidOperatorId, reason);

        // Assert
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_HIDE_REASON_TOO_LONG");
    }

    #endregion

    #region State Machine - Full Transitions

    [Fact]
    public void FullLifecycle_PendingToApprovedToHidden_ShouldTransitionCorrectly()
    {
        // Arrange
        var review = CreatePendingReview();

        // Act & Assert - Pending -> Approved
        review.Approve(ValidAuditorId);
        review.Status.Should().Be(ReviewStatus.Approved);

        // Act & Assert - Approved -> Hidden
        review.Hide(ValidOperatorId, "Violation");
        review.Status.Should().Be(ReviewStatus.Hidden);
    }

    [Fact]
    public void FullLifecycle_WithSellerReply_ShouldCompleteAllSteps()
    {
        // Arrange
        var review = CreatePendingReview();

        // Act - Approve
        review.Approve(ValidAuditorId);
        review.Status.Should().Be(ReviewStatus.Approved);

        // Act - Seller reply
        review.SellerReply("Thank you!");
        review.SellerReplyContent.Should().Be("Thank you!");

        // Act - Hide
        review.Hide(ValidOperatorId, "Violation");
        review.Status.Should().Be(ReviewStatus.Hidden);

        // Act - Seller reply should fail after hidden
        var act = () => review.SellerReply("Another reply");
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "REVIEW_REPLY_STATUS_INVALID");
    }

    #endregion

    #region Domain Events - Multiple Events

    [Fact]
    public void DomainEvents_AfterFullLifecycle_ShouldContainMultipleEvents()
    {
        // Arrange
        var review = CreatePendingReview();
        review.ClearDomainEvents();

        // Act
        review.Approve(ValidAuditorId);
        review.Hide(ValidOperatorId, "Violation");

        // Assert
        review.DomainEvents.Should().HaveCount(2);
        review.DomainEvents.OfType<ReviewApprovedDomainEvent>().Should().HaveCount(1);
        review.DomainEvents.OfType<ReviewHiddenDomainEvent>().Should().HaveCount(1);
    }

    [Fact]
    public void ClearDomainEvents_ShouldClearAllEvents()
    {
        // Arrange
        var review = CreatePendingReview();

        // Act
        review.ClearDomainEvents();

        // Assert
        review.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private static Review CreatePendingReview()
    {
        return Review.Create(
            ValidReviewId, ValidOrderId, ValidOrderLineId,
            ValidSpuId, ValidSkuId, ValidUserId,
            4, "A valid review content.", new List<string> { "img1.jpg" });
    }

    private static Review CreateApprovedReview()
    {
        var review = CreatePendingReview();
        review.Approve(ValidAuditorId);
        return review;
    }

    private static Review CreateHiddenReview()
    {
        var review = CreateApprovedReview();
        review.Hide(ValidOperatorId, "Violation of policy");
        return review;
    }

    #endregion
}