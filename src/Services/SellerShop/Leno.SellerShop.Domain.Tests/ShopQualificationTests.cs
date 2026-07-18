using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Entities;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Domain.Tests;

public class ShopQualificationTests
{
    private static readonly Guid ValidQualificationId = Guid.NewGuid();
    private static readonly Guid ValidShopId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private static readonly Guid ValidReviewerId = Guid.NewGuid();
    private const string ValidNumber = "LIC123456789";
    private const string ValidImageUrl = "https://storage.example.com/qualifications/lic123.jpg";
    private const string ValidShopName = "Test Shop";
    private const string ValidPhone = "13800138000";
    private const string ValidReason = "Valid reason";

    // ==================== ShopQualification.Create ====================

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var validTo = new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var qualification = ShopQualification.Create(
            ValidQualificationId,
            ValidShopId,
            QualificationType.BusinessLicense,
            ValidNumber,
            ValidImageUrl,
            validFrom,
            validTo);

        qualification.Id.Should().Be(ValidQualificationId);
        qualification.ShopId.Should().Be(ValidShopId);
        qualification.Type.Should().Be(QualificationType.BusinessLicense);
        qualification.Number.Should().Be(ValidNumber);
        qualification.ImageUrl.Should().Be(ValidImageUrl);
        qualification.ValidFrom.Should().Be(validFrom);
        qualification.ValidTo.Should().Be(validTo);
        qualification.Status.Should().Be(QualificationStatus.Pending);
        qualification.RejectReason.Should().BeNull();
        qualification.ReviewedBy.Should().BeNull();
    }

    [Fact]
    public void Create_WithFoodSafetyType_ShouldSucceed()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.FoodLicense,
            "FS123456", ValidImageUrl, validFrom, validTo);

        qualification.Type.Should().Be(QualificationType.FoodLicense);
    }

    [Fact]
    public void Create_WithOtherType_ShouldSucceed()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.Other,
            "OTHER-001", ValidImageUrl, validFrom, validTo);

        qualification.Type.Should().Be(QualificationType.Other);
    }

    [Fact]
    public void Create_WithEmptyQualificationId_ShouldThrowQualificationIdEmpty()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var act = () => ShopQualification.Create(
            Guid.Empty, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, validFrom, validTo);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyShopId_ShouldThrowQualificationShopEmpty()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var act = () => ShopQualification.Create(
            ValidQualificationId, Guid.Empty, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, validFrom, validTo);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_SHOP_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyNumber_ShouldThrowQualificationNumberEmpty()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var act = () => ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            "", ValidImageUrl, validFrom, validTo);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_NUMBER_EMPTY");
    }

    [Fact]
    public void Create_WithWhitespaceNumber_ShouldThrowQualificationNumberEmpty()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var act = () => ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            "   ", ValidImageUrl, validFrom, validTo);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_NUMBER_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyImageUrl_ShouldThrowQualificationImageEmpty()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var act = () => ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, "", validFrom, validTo);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_IMAGE_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongNumber_ShouldThrowQualificationNumberLength()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var act = () => ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            new string('N', 65), ValidImageUrl, validFrom, validTo);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_NUMBER_LENGTH");
    }

    [Fact]
    public void Create_WithMaxNumberLength_ShouldSucceed()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);
        var number = new string('N', 64);

        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            number, ValidImageUrl, validFrom, validTo);

        qualification.Number.Should().Be(number);
    }

    [Fact]
    public void Create_WithTooLongImageUrl_ShouldThrowQualificationImageLength()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var act = () => ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, new string('U', 513), validFrom, validTo);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_IMAGE_LENGTH");
    }

    [Fact]
    public void Create_WithMaxImageUrlLength_ShouldSucceed()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);
        var imageUrl = new string('U', 512);

        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, imageUrl, validFrom, validTo);

        qualification.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public void Create_WithValidFromEqualToValidTo_ShouldThrowQualificationDateInvalid()
    {
        var date = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var act = () => ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, date, date);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_DATE_INVALID");
    }

    [Fact]
    public void Create_WithValidFromAfterValidTo_ShouldThrowQualificationDateInvalid()
    {
        var validFrom = new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var validTo = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var act = () => ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, validFrom, validTo);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_DATE_INVALID");
    }

    [Fact]
    public void Create_ShouldTrimNumberAndImageUrl()
    {
        var validFrom = DateTime.UtcNow;
        var validTo = DateTime.UtcNow.AddYears(1);

        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            "  " + ValidNumber + "  ", "  " + ValidImageUrl + "  ", validFrom, validTo);

        qualification.Number.Should().Be(ValidNumber);
        qualification.ImageUrl.Should().Be(ValidImageUrl);
    }

    // ==================== ShopQualification.Approve ====================

    private static ShopQualification CreatePendingQualification()
    {
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var validTo = new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, validFrom, validTo);
    }

    [Fact]
    public void Approve_WhenPending_ShouldSetApproved()
    {
        var qualification = CreatePendingQualification();

        qualification.Approve(ValidReviewerId);

        qualification.Status.Should().Be(QualificationStatus.Approved);
        qualification.ReviewedBy.Should().Be(ValidReviewerId);
        qualification.RejectReason.Should().BeNull();
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ShouldThrowInvalidTransition()
    {
        var qualification = CreatePendingQualification();
        qualification.Approve(ValidReviewerId);

        var act = () => qualification.Approve(ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_INVALID_TRANSITION");
    }

    [Fact]
    public void Approve_WhenRejected_ShouldThrowInvalidTransition()
    {
        var qualification = CreatePendingQualification();
        qualification.Reject(ValidReviewerId, ValidReason);

        var act = () => qualification.Approve(ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_INVALID_TRANSITION");
    }

    [Fact]
    public void Approve_WithEmptyReviewer_ShouldThrowQualificationReviewerEmpty()
    {
        var qualification = CreatePendingQualification();

        var act = () => qualification.Approve(Guid.Empty);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_REVIEWER_EMPTY");
    }

    // ==================== ShopQualification.Reject ====================

    [Fact]
    public void Reject_WhenPending_ShouldSetRejected()
    {
        var qualification = CreatePendingQualification();

        qualification.Reject(ValidReviewerId, ValidReason);

        qualification.Status.Should().Be(QualificationStatus.Rejected);
        qualification.ReviewedBy.Should().Be(ValidReviewerId);
        qualification.RejectReason.Should().Be(ValidReason);
    }

    [Fact]
    public void Reject_WhenAlreadyApproved_ShouldThrowInvalidTransition()
    {
        var qualification = CreatePendingQualification();
        qualification.Approve(ValidReviewerId);

        var act = () => qualification.Reject(ValidReviewerId, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_INVALID_TRANSITION");
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_ShouldThrowInvalidTransition()
    {
        var qualification = CreatePendingQualification();
        qualification.Reject(ValidReviewerId, ValidReason);

        var act = () => qualification.Reject(ValidReviewerId, "Another reason");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_INVALID_TRANSITION");
    }

    [Fact]
    public void Reject_WithEmptyReviewer_ShouldThrowQualificationReviewerEmpty()
    {
        var qualification = CreatePendingQualification();

        var act = () => qualification.Reject(Guid.Empty, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_REVIEWER_EMPTY");
    }

    [Fact]
    public void Reject_WithEmptyReason_ShouldThrowQualificationReasonEmpty()
    {
        var qualification = CreatePendingQualification();

        var act = () => qualification.Reject(ValidReviewerId, "");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_REASON_EMPTY");
    }

    [Fact]
    public void Reject_WithWhitespaceReason_ShouldThrowQualificationReasonEmpty()
    {
        var qualification = CreatePendingQualification();

        var act = () => qualification.Reject(ValidReviewerId, "   ");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_REASON_EMPTY");
    }

    [Fact]
    public void Reject_WithTooLongReason_ShouldThrowQualificationReasonLength()
    {
        var qualification = CreatePendingQualification();

        var act = () => qualification.Reject(ValidReviewerId, new string('R', 201));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_REASON_LENGTH");
    }

    [Fact]
    public void Reject_WithMaxReasonLength_ShouldSucceed()
    {
        var qualification = CreatePendingQualification();
        var reason = new string('R', 200);

        qualification.Reject(ValidReviewerId, reason);

        qualification.RejectReason.Should().Be(reason);
    }

    // ==================== ShopQualification.IsExpiringWithin ====================

    [Fact]
    public void IsExpiringWithin_WhenApprovedAndExpiringIn30Days_ShouldReturnTrue()
    {
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var validTo = new DateTime(2025, 7, 12, 0, 0, 0, DateTimeKind.Utc);
        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, validFrom, validTo);
        qualification.Approve(ValidReviewerId);

        // validTo is 2025-07-12, utcNow is 2025-06-12, 30 days remaining
        var utcNow = new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Utc);
        qualification.IsExpiringWithin(30, utcNow).Should().BeTrue();
    }

    [Fact]
    public void IsExpiringWithin_WhenApprovedAndExpiringIn7Days_ShouldReturnTrue()
    {
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var validTo = new DateTime(2025, 6, 19, 0, 0, 0, DateTimeKind.Utc);
        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, validFrom, validTo);
        qualification.Approve(ValidReviewerId);

        // validTo is 2025-06-19, utcNow is 2025-06-12, 7 days remaining
        var utcNow = new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Utc);
        qualification.IsExpiringWithin(7, utcNow).Should().BeTrue();
    }

    [Fact]
    public void IsExpiringWithin_WhenApprovedAndExpiringIn1Day_ShouldReturnTrue()
    {
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var validTo = new DateTime(2025, 6, 13, 0, 0, 0, DateTimeKind.Utc);
        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, validFrom, validTo);
        qualification.Approve(ValidReviewerId);

        // validTo is 2025-06-13, utcNow is 2025-06-12, 1 day remaining
        var utcNow = new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Utc);
        qualification.IsExpiringWithin(1, utcNow).Should().BeTrue();
    }

    [Fact]
    public void IsExpiringWithin_WhenApprovedButNotExpiringSoon_ShouldReturnFalse()
    {
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var validTo = new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, validFrom, validTo);
        qualification.Approve(ValidReviewerId);

        var utcNow = new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Utc);
        qualification.IsExpiringWithin(30, utcNow).Should().BeFalse();
    }

    [Fact]
    public void IsExpiringWithin_WhenPending_ShouldReturnFalse()
    {
        var qualification = CreatePendingQualification();
        var utcNow = DateTime.UtcNow;

        qualification.IsExpiringWithin(30, utcNow).Should().BeFalse();
    }

    [Fact]
    public void IsExpiringWithin_WhenRejected_ShouldReturnFalse()
    {
        var qualification = CreatePendingQualification();
        qualification.Reject(ValidReviewerId, ValidReason);
        var utcNow = DateTime.UtcNow;

        qualification.IsExpiringWithin(30, utcNow).Should().BeFalse();
    }

    [Fact]
    public void IsExpiringWithin_WhenAlreadyExpired_ShouldReturnFalse()
    {
        var validFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var validTo = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var qualification = ShopQualification.Create(
            ValidQualificationId, ValidShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, validFrom, validTo);
        qualification.Approve(ValidReviewerId);

        // validTo is 2025-06-01, utcNow is 2025-06-12, already expired
        var utcNow = new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Utc);
        qualification.IsExpiringWithin(30, utcNow).Should().BeFalse();
    }

    // ==================== Shop.AddQualification ====================

    private static Shop CreateActiveShop()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidShopName, ValidPhone);
        shop.Approve(ValidReviewerId);
        return shop;
    }

    [Fact]
    public void AddQualification_WhenActive_ShouldAddToList()
    {
        var shop = CreateActiveShop();
        var qualification = CreatePendingQualification();

        shop.AddQualification(qualification);

        shop.Qualifications.Should().ContainSingle()
            .Which.Should().Be(qualification);
    }

    [Fact]
    public void AddQualification_WithMismatchedShopId_ShouldThrowQualificationShopMismatch()
    {
        var shop = CreateActiveShop();
        var otherShopId = Guid.NewGuid();
        var qualification = ShopQualification.Create(
            Guid.NewGuid(), otherShopId, QualificationType.BusinessLicense,
            ValidNumber, ValidImageUrl, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        var act = () => shop.AddQualification(qualification);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_SHOP_MISMATCH");
    }

    [Fact]
    public void AddQualification_WhenClosed_ShouldThrowShopClosed()
    {
        var shop = CreateActiveShop();
        shop.Close("Closing");
        var qualification = CreatePendingQualification();

        var act = () => shop.AddQualification(qualification);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_CLOSED");
    }

    [Fact]
    public void AddQualification_WithNull_ShouldThrowArgumentNullException()
    {
        var shop = CreateActiveShop();

        var act = () => shop.AddQualification(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddQualification_Multiple_ShouldAddAll()
    {
        var shop = CreateActiveShop();
        var q1 = CreatePendingQualification();
        var q2 = ShopQualification.Create(
            Guid.NewGuid(), ValidShopId, QualificationType.FoodLicense,
            "FS123", ValidImageUrl, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        shop.AddQualification(q1);
        shop.AddQualification(q2);

        shop.Qualifications.Should().HaveCount(2);
    }

    // ==================== Shop.GetQualification ====================

    [Fact]
    public void GetQualification_WhenExists_ShouldReturnQualification()
    {
        var shop = CreateActiveShop();
        var qualification = CreatePendingQualification();
        shop.AddQualification(qualification);

        var result = shop.GetQualification(ValidQualificationId);

        result.Should().NotBeNull();
        result.Should().Be(qualification);
    }

    [Fact]
    public void GetQualification_WhenNotExists_ShouldReturnNull()
    {
        var shop = CreateActiveShop();

        var result = shop.GetQualification(Guid.NewGuid());

        result.Should().BeNull();
    }

    // ==================== Shop.ApproveQualification ====================

    [Fact]
    public void ApproveQualification_WhenExists_ShouldApprove()
    {
        var shop = CreateActiveShop();
        var qualification = CreatePendingQualification();
        shop.AddQualification(qualification);

        shop.ApproveQualification(ValidQualificationId, ValidReviewerId);

        qualification.Status.Should().Be(QualificationStatus.Approved);
    }

    [Fact]
    public void ApproveQualification_WhenNotExists_ShouldThrowQualificationNotFound()
    {
        var shop = CreateActiveShop();

        var act = () => shop.ApproveQualification(Guid.NewGuid(), ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_NOT_FOUND");
    }

    // ==================== Shop.RejectQualification ====================

    [Fact]
    public void RejectQualification_WhenExists_ShouldReject()
    {
        var shop = CreateActiveShop();
        var qualification = CreatePendingQualification();
        shop.AddQualification(qualification);

        shop.RejectQualification(ValidQualificationId, ValidReviewerId, ValidReason);

        qualification.Status.Should().Be(QualificationStatus.Rejected);
        qualification.RejectReason.Should().Be(ValidReason);
    }

    [Fact]
    public void RejectQualification_WhenNotExists_ShouldThrowQualificationNotFound()
    {
        var shop = CreateActiveShop();

        var act = () => shop.RejectQualification(Guid.NewGuid(), ValidReviewerId, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_NOT_FOUND");
    }
}