using Leno.SharedContracts.Events;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Entities;
using Leno.SellerShop.Domain.Events;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Domain.Tests;

public class ShopTests
{
    private static readonly Guid ValidShopId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private static readonly Guid ValidReviewerId = Guid.NewGuid();
    private const string ValidName = "Test Shop";
    private const string ValidPhone = "13800138000";
    private const string ValidEmail = "test@shop.com";
    private const string ValidReason = "Valid reason";

    // ==================== Create factory ====================

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone,
            ValidEmail, "A great shop", "https://logo.url", "LIC123456", "123 Main St");

        shop.Id.Should().Be(ValidShopId);
        shop.SellerId.Should().Be(ValidUserId);
        shop.ShopName.Should().Be(ValidName);
        shop.ContactPhone.Should().Be(ValidPhone);
        shop.ContactEmail.Should().Be(ValidEmail);
        shop.Description.Should().Be("A great shop");
        shop.Logo.Should().Be("https://logo.url");
        shop.BusinessLicenseNo.Should().Be("LIC123456");
        shop.Address.Should().Be("123 Main St");
        shop.Status.Should().Be(ShopStatus.PendingReview);
        shop.ProductCount.Should().Be(0);
    }

    [Fact]
    public void Create_WithNullOptionalFields_ShouldSetNulls()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        shop.ContactEmail.Should().BeNull();
        shop.Description.Should().BeNull();
        shop.Logo.Should().BeNull();
        shop.BusinessLicenseNo.Should().BeNull();
        shop.Address.Should().BeNull();
    }

    [Fact]
    public void Create_WithWhitespaceOptionalFields_ShouldTrimToNull()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone,
            "   ", "   ", "   ", "   ", "   ");

        shop.ContactEmail.Should().BeNull();
        shop.Description.Should().BeNull();
        shop.Logo.Should().BeNull();
        shop.BusinessLicenseNo.Should().BeNull();
        shop.Address.Should().BeNull();
    }

    [Fact]
    public void Create_WithMinShopNameLength_ShouldSucceed()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, "AB", ValidPhone);
        shop.ShopName.Should().Be("AB");
    }

    [Fact]
    public void Create_WithMaxShopNameLength_ShouldSucceed()
    {
        var name = new string('A', 32);
        var shop = Shop.Create(ValidShopId, ValidUserId, name, ValidPhone);
        shop.ShopName.Should().Be(name);
    }

    [Fact]
    public void Create_WithMaxDescriptionLength_ShouldSucceed()
    {
        var desc = new string('D', 1000);
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone, description: desc);
        shop.Description.Should().Be(desc);
    }

    [Fact]
    public void Create_WithMaxAddressLength_ShouldSucceed()
    {
        var addr = new string('A', 256);
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone, address: addr);
        shop.Address.Should().Be(addr);
    }

    [Fact]
    public void Create_WithMaxPhoneLength_ShouldSucceed()
    {
        var phone = new string('1', 20);
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, phone);
        shop.ContactPhone.Should().Be(phone);
    }

    [Fact]
    public void Create_WithMaxEmailLength_ShouldSucceed()
    {
        // MaxEmailLength = 256. Email format: "a@" + domain + ".com" => 1 + 1 + domain + 4 = 6 + domain
        // So domain max = 256 - 6 = 250
        var domain = new string('a', 250);
        var email = "a@" + domain + ".com";
        email.Length.Should().Be(256);
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone, contactEmail: email);
        shop.ContactEmail.Should().Be(email);
    }

    [Fact]
    public void Create_WithMaxLogoLength_ShouldSucceed()
    {
        var logo = new string('L', 512);
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone, logo: logo);
        shop.Logo.Should().Be(logo);
    }

    [Fact]
    public void Create_WithMaxLicenseNoLength_ShouldSucceed()
    {
        var license = new string('L', 32);
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone, businessLicenseNo: license);
        shop.BusinessLicenseNo.Should().Be(license);
    }

    // ==================== Create validation guards ====================

    [Fact]
    public void Create_WithEmptyShopId_ShouldThrowShopIdEmpty()
    {
        var act = () => Shop.Create(Guid.Empty, ValidUserId, ValidName, ValidPhone);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowShopSellerEmpty()
    {
        var act = () => Shop.Create(ValidShopId, Guid.Empty, ValidName, ValidPhone);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_SELLER_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyShopName_ShouldThrowShopNameEmpty()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, "", ValidPhone);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithWhitespaceShopName_ShouldThrowShopNameEmpty()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, "   ", ValidPhone);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithTooShortShopName_ShouldThrowShopNameLength()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, "A", ValidPhone);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_NAME_LENGTH");
    }

    [Fact]
    public void Create_WithTooLongShopName_ShouldThrowShopNameLength()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, new string('A', 33), ValidPhone);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_NAME_LENGTH");
    }

    [Fact]
    public void Create_WithEmptyPhone_ShouldThrowShopPhoneEmpty()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, "");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_PHONE_EMPTY");
    }

    [Fact]
    public void Create_WithWhitespacePhone_ShouldThrowShopPhoneEmpty()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, "   ");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_PHONE_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongPhone_ShouldThrowShopPhoneLength()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, new string('1', 21));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_PHONE_LENGTH");
    }

    [Fact]
    public void Create_WithTooLongEmail_ShouldThrowShopEmailLength()
    {
        // MaxEmailLength = 256, so 257 chars should fail
        var domain = new string('a', 251);
        var email = "a@" + domain + ".com";
        email.Length.Should().Be(257);
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone, contactEmail: email);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_EMAIL_LENGTH");
    }

    [Fact]
    public void Create_WithEmailWithoutAt_ShouldThrowShopEmailFormat()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone, contactEmail: "noatsign.com");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_EMAIL_FORMAT");
    }

    [Fact]
    public void Create_WithEmailStartingWithAt_ShouldThrowShopEmailFormat()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone, contactEmail: "@domain.com");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_EMAIL_FORMAT");
    }

    [Fact]
    public void Create_WithEmailEndingWithAt_ShouldThrowShopEmailFormat()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone, contactEmail: "username@");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_EMAIL_FORMAT");
    }

    [Fact]
    public void Create_WithTooLongDescription_ShouldThrowShopDescriptionLength()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone,
            description: new string('D', 1001));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_DESCRIPTION_LENGTH");
    }

    [Fact]
    public void Create_WithTooLongLogo_ShouldThrowShopLogoLength()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone,
            logo: new string('L', 513));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_LOGO_LENGTH");
    }

    [Fact]
    public void Create_WithTooLongLicenseNo_ShouldThrowShopLicenseLength()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone,
            businessLicenseNo: new string('L', 33));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_LICENSE_LENGTH");
    }

    [Fact]
    public void Create_WithTooLongAddress_ShouldThrowShopAddressLength()
    {
        var act = () => Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone,
            address: new string('A', 257));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_ADDRESS_LENGTH");
    }

    [Fact]
    public void Create_ShouldTrimFieldValues()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, "  " + ValidName + "  ", "  " + ValidPhone + "  ",
            "  " + ValidEmail + "  ");

        shop.ShopName.Should().Be(ValidName);
        shop.ContactPhone.Should().Be(ValidPhone);
        shop.ContactEmail.Should().Be(ValidEmail);
    }

    // ==================== Domain events on create ====================

    [Fact]
    public void Create_ShouldRaiseSellerRegisteredEvent()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        shop.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SellerRegisteredDomainEvent>()
            .Which.Should().Match<SellerRegisteredDomainEvent>(e =>
                e.ShopId == ValidShopId &&
                e.SellerId == ValidUserId &&
                e.UserId == ValidUserId &&
                e.ShopName == ValidName);
    }

    // ==================== Approve ====================

    [Fact]
    public void Approve_WhenPendingReview_ShouldSetActiveAndRaiseEvent()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);
        shop.ClearDomainEvents();

        shop.Approve(ValidReviewerId);

        shop.Status.Should().Be(ShopStatus.Active);
        shop.ReviewedBy.Should().Be(ValidReviewerId);
        shop.StatusReason.Should().BeNull();
        shop.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShopApprovedDomainEvent>()
            .Which.Should().Match<ShopApprovedDomainEvent>(e =>
                e.ShopId == ValidShopId &&
                e.SellerId == ValidUserId &&
                e.ShopName == ValidName);
    }

    [Fact]
    public void Approve_WhenActive_ShouldThrowInvalidTransition()
    {
        var shop = CreateActiveShop();
        shop.ClearDomainEvents();

        var act = () => shop.Approve(ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void Approve_WhenSuspended_ShouldThrowInvalidTransition()
    {
        var shop = CreateSuspendedShop();
        shop.ClearDomainEvents();

        var act = () => shop.Approve(ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Approve_WhenRejected_ShouldThrowInvalidTransition()
    {
        var shop = CreateRejectedShop();
        shop.ClearDomainEvents();

        var act = () => shop.Approve(ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Approve_WhenClosed_ShouldThrowInvalidTransition()
    {
        var shop = CreateClosedShop();
        shop.ClearDomainEvents();

        var act = () => shop.Approve(ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Approve_WithEmptyReviewer_ShouldThrowShopReviewerEmpty()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        var act = () => shop.Approve(Guid.Empty);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_REVIEWER_EMPTY");
    }

    // ==================== Reject ====================

    [Fact]
    public void Reject_WhenPendingReview_ShouldSetRejected()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        shop.Reject(ValidReviewerId, ValidReason);

        shop.Status.Should().Be(ShopStatus.Rejected);
        shop.ReviewedBy.Should().Be(ValidReviewerId);
        shop.StatusReason.Should().Be(ValidReason);
    }

    [Fact]
    public void Reject_WhenActive_ShouldThrowInvalidTransition()
    {
        var shop = CreateActiveShop();

        var act = () => shop.Reject(ValidReviewerId, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void Reject_WhenSuspended_ShouldThrowInvalidTransition()
    {
        var shop = CreateSuspendedShop();

        var act = () => shop.Reject(ValidReviewerId, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Reject_WhenRejected_ShouldThrowInvalidTransition()
    {
        var shop = CreateRejectedShop();

        var act = () => shop.Reject(ValidReviewerId, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Reject_WhenClosed_ShouldThrowInvalidTransition()
    {
        var shop = CreateClosedShop();

        var act = () => shop.Reject(ValidReviewerId, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Reject_WithEmptyReviewer_ShouldThrowShopReviewerEmpty()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        var act = () => shop.Reject(Guid.Empty, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_REVIEWER_EMPTY");
    }

    [Fact]
    public void Reject_WithEmptyReason_ShouldThrowShopReasonEmpty()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        var act = () => shop.Reject(ValidReviewerId, "");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_REASON_EMPTY");
    }

    [Fact]
    public void Reject_WithWhitespaceReason_ShouldThrowShopReasonEmpty()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        var act = () => shop.Reject(ValidReviewerId, "   ");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_REASON_EMPTY");
    }

    [Fact]
    public void Reject_WithTooLongReason_ShouldThrowShopReasonLength()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        var act = () => shop.Reject(ValidReviewerId, new string('R', 201));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_REASON_LENGTH");
    }

    [Fact]
    public void Reject_WithMaxReasonLength_ShouldSucceed()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);
        var reason = new string('R', 200);

        shop.Reject(ValidReviewerId, reason);

        shop.StatusReason.Should().Be(reason);
    }

    // ==================== Suspend ====================

    [Fact]
    public void Suspend_WhenActive_ShouldSetSuspendedAndRaiseEvent()
    {
        var shop = CreateActiveShop();
        shop.ClearDomainEvents();

        shop.Suspend(ValidReason);

        shop.Status.Should().Be(ShopStatus.Suspended);
        shop.StatusReason.Should().Be(ValidReason);
        shop.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShopSuspendedDomainEvent>()
            .Which.Should().Match<ShopSuspendedDomainEvent>(e =>
                e.ShopId == ValidShopId &&
                e.SellerId == ValidUserId);
    }

    [Fact]
    public void Suspend_WhenPendingReview_ShouldThrowInvalidTransition()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        var act = () => shop.Suspend(ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void Suspend_WhenSuspended_ShouldThrowInvalidTransition()
    {
        var shop = CreateSuspendedShop();

        var act = () => shop.Suspend(ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Suspend_WhenRejected_ShouldThrowInvalidTransition()
    {
        var shop = CreateRejectedShop();

        var act = () => shop.Suspend(ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Suspend_WhenClosed_ShouldThrowInvalidTransition()
    {
        var shop = CreateClosedShop();

        var act = () => shop.Suspend(ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Suspend_WithEmptyReason_ShouldThrowShopReasonEmpty()
    {
        var shop = CreateActiveShop();

        var act = () => shop.Suspend("");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_REASON_EMPTY");
    }

    [Fact]
    public void Suspend_WithTooLongReason_ShouldThrowShopReasonLength()
    {
        var shop = CreateActiveShop();

        var act = () => shop.Suspend(new string('R', 201));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_REASON_LENGTH");
    }

    // ==================== Resume ====================

    [Fact]
    public void Resume_WhenSuspended_ShouldSetActiveAndRaiseEvent()
    {
        var shop = CreateSuspendedShop();
        shop.ClearDomainEvents();

        shop.Resume();

        shop.Status.Should().Be(ShopStatus.Active);
        shop.StatusReason.Should().BeNull();
        shop.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShopResumedDomainEvent>()
            .Which.Should().Match<ShopResumedDomainEvent>(e =>
                e.ShopId == ValidShopId &&
                e.SellerId == ValidUserId);
    }

    [Fact]
    public void Resume_WhenPendingReview_ShouldThrowInvalidTransition()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        var act = () => shop.Resume();
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void Resume_WhenActive_ShouldThrowInvalidTransition()
    {
        var shop = CreateActiveShop();

        var act = () => shop.Resume();
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Resume_WhenRejected_ShouldThrowInvalidTransition()
    {
        var shop = CreateRejectedShop();

        var act = () => shop.Resume();
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    [Fact]
    public void Resume_WhenClosed_ShouldThrowInvalidTransition()
    {
        var shop = CreateClosedShop();

        var act = () => shop.Resume();
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_INVALID_TRANSITION");
    }

    // ==================== Close ====================

    [Fact]
    public void Close_WhenActive_ShouldSetClosedAndRaiseEvent()
    {
        var shop = CreateActiveShop();
        shop.ClearDomainEvents();

        shop.Close(ValidReason);

        shop.Status.Should().Be(ShopStatus.Closed);
        shop.StatusReason.Should().Be(ValidReason);
        shop.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShopClosedDomainEvent>()
            .Which.Should().Match<ShopClosedDomainEvent>(e =>
                e.ShopId == ValidShopId &&
                e.SellerId == ValidUserId);
    }

    [Fact]
    public void Close_WhenPendingReview_ShouldSetClosed()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        shop.Close(ValidReason);

        shop.Status.Should().Be(ShopStatus.Closed);
    }

    [Fact]
    public void Close_WhenSuspended_ShouldSetClosed()
    {
        var shop = CreateSuspendedShop();

        shop.Close(ValidReason);

        shop.Status.Should().Be(ShopStatus.Closed);
    }

    [Fact]
    public void Close_WhenRejected_ShouldSetClosed()
    {
        var shop = CreateRejectedShop();

        shop.Close(ValidReason);

        shop.Status.Should().Be(ShopStatus.Closed);
    }

    [Fact]
    public void Close_WhenAlreadyClosed_ShouldThrowAlreadyClosed()
    {
        var shop = CreateClosedShop();

        var act = () => shop.Close(ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_ALREADY_CLOSED");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void Close_WithEmptyReason_ShouldThrowShopReasonEmpty()
    {
        var shop = CreateActiveShop();

        var act = () => shop.Close("");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_REASON_EMPTY");
    }

    [Fact]
    public void Close_WithTooLongReason_ShouldThrowShopReasonLength()
    {
        var shop = CreateActiveShop();

        var act = () => shop.Close(new string('R', 201));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_REASON_LENGTH");
    }

    [Fact]
    public void Close_WithMaxReasonLength_ShouldSucceed()
    {
        var shop = CreateActiveShop();
        var reason = new string('R', 200);

        shop.Close(reason);

        shop.Status.Should().Be(ShopStatus.Closed);
        shop.StatusReason.Should().Be(reason);
    }

    // ==================== UpdateInfo ====================

    [Fact]
    public void UpdateInfo_WhenActive_ShouldUpdateFields()
    {
        var shop = CreateActiveShop();

        shop.UpdateInfo("New Name", "New Desc", "New Address");

        shop.ShopName.Should().Be("New Name");
        shop.Description.Should().Be("New Desc");
        shop.Address.Should().Be("New Address");
    }

    [Fact]
    public void UpdateInfo_WithNullOptionalFields_ShouldSetNulls()
    {
        var shop = CreateActiveShop();

        shop.UpdateInfo("New Name", null, null);

        shop.ShopName.Should().Be("New Name");
        shop.Description.Should().BeNull();
        shop.Address.Should().BeNull();
    }

    [Fact]
    public void UpdateInfo_WithWhitespaceOptionalFields_ShouldTrimToNull()
    {
        var shop = CreateActiveShop();

        shop.UpdateInfo("New Name", "   ", "   ");

        shop.Description.Should().BeNull();
        shop.Address.Should().BeNull();
    }

    [Fact]
    public void UpdateInfo_WhenClosed_ShouldThrowShopClosed()
    {
        var shop = CreateClosedShop();

        var act = () => shop.UpdateInfo("New Name", null, null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_CLOSED");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void UpdateInfo_WithEmptyName_ShouldThrowShopNameEmpty()
    {
        var shop = CreateActiveShop();

        var act = () => shop.UpdateInfo("", null, null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_NAME_EMPTY");
    }

    [Fact]
    public void UpdateInfo_WithTooLongName_ShouldThrowShopNameLength()
    {
        var shop = CreateActiveShop();

        var act = () => shop.UpdateInfo(new string('N', 33), null, null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_NAME_LENGTH");
    }

    [Fact]
    public void UpdateInfo_WithTooLongDescription_ShouldThrowShopDescriptionLength()
    {
        var shop = CreateActiveShop();

        var act = () => shop.UpdateInfo("Valid Name", new string('D', 1001), null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_DESCRIPTION_LENGTH");
    }

    [Fact]
    public void UpdateInfo_WithTooLongAddress_ShouldThrowShopAddressLength()
    {
        var shop = CreateActiveShop();

        var act = () => shop.UpdateInfo("Valid Name", null, new string('A', 257));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_ADDRESS_LENGTH");
    }

    // ==================== UpdateLogo ====================

    [Fact]
    public void UpdateLogo_WhenActive_ShouldUpdateLogo()
    {
        var shop = CreateActiveShop();

        shop.UpdateLogo("https://new-logo.url");

        shop.Logo.Should().Be("https://new-logo.url");
    }

    [Fact]
    public void UpdateLogo_WithNull_ShouldSetNull()
    {
        var shop = CreateActiveShop();
        shop.UpdateLogo(null);

        shop.Logo.Should().BeNull();
    }

    [Fact]
    public void UpdateLogo_WithWhitespace_ShouldTrimToNull()
    {
        var shop = CreateActiveShop();
        shop.UpdateLogo("   ");

        shop.Logo.Should().BeNull();
    }

    [Fact]
    public void UpdateLogo_WhenClosed_ShouldThrowShopClosed()
    {
        var shop = CreateClosedShop();

        var act = () => shop.UpdateLogo("https://new-logo.url");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_CLOSED");
    }

    [Fact]
    public void UpdateLogo_WithTooLongLogo_ShouldThrowShopLogoLength()
    {
        var shop = CreateActiveShop();

        var act = () => shop.UpdateLogo(new string('L', 513));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_LOGO_LENGTH");
    }

    // ==================== UpdateContact ====================

    [Fact]
    public void UpdateContact_WhenActive_ShouldUpdateContact()
    {
        var shop = CreateActiveShop();

        shop.UpdateContact("13900139000", "new@shop.com");

        shop.ContactPhone.Should().Be("13900139000");
        shop.ContactEmail.Should().Be("new@shop.com");
    }

    [Fact]
    public void UpdateContact_WithNullEmail_ShouldSetNull()
    {
        var shop = CreateActiveShop();

        shop.UpdateContact("13900139000", null);

        shop.ContactPhone.Should().Be("13900139000");
        shop.ContactEmail.Should().BeNull();
    }

    [Fact]
    public void UpdateContact_WithWhitespaceEmail_ShouldTrimToNull()
    {
        var shop = CreateActiveShop();

        shop.UpdateContact("13900139000", "   ");

        shop.ContactEmail.Should().BeNull();
    }

    [Fact]
    public void UpdateContact_WhenClosed_ShouldThrowShopClosed()
    {
        var shop = CreateClosedShop();

        var act = () => shop.UpdateContact("13900139000", "new@shop.com");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_CLOSED");
    }

    [Fact]
    public void UpdateContact_WithEmptyPhone_ShouldThrowShopPhoneEmpty()
    {
        var shop = CreateActiveShop();

        var act = () => shop.UpdateContact("", null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_PHONE_EMPTY");
    }

    [Fact]
    public void UpdateContact_WithTooLongPhone_ShouldThrowShopPhoneLength()
    {
        var shop = CreateActiveShop();

        var act = () => shop.UpdateContact(new string('1', 21), null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_PHONE_LENGTH");
    }

    [Fact]
    public void UpdateContact_WithInvalidEmail_ShouldThrowShopEmailFormat()
    {
        var shop = CreateActiveShop();

        var act = () => shop.UpdateContact("13900139000", "noatsign.com");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_EMAIL_FORMAT");
    }

    // ==================== ProductCount ====================

    [Fact]
    public void IncrementProductCount_ShouldIncreaseByOne()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        shop.IncrementProductCount();
        shop.ProductCount.Should().Be(1);

        shop.IncrementProductCount();
        shop.ProductCount.Should().Be(2);
    }

    [Fact]
    public void DecrementProductCount_WhenGreaterThanZero_ShouldDecreaseByOne()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);
        shop.IncrementProductCount();
        shop.IncrementProductCount();

        shop.DecrementProductCount();
        shop.ProductCount.Should().Be(1);

        shop.DecrementProductCount();
        shop.ProductCount.Should().Be(0);
    }

    [Fact]
    public void DecrementProductCount_WhenZero_ShouldStayZero()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        shop.DecrementProductCount();
        shop.ProductCount.Should().Be(0);
    }

    // ==================== Full state machine workflows ====================

    [Fact]
    public void FullWorkflow_PendingReviewToActiveToSuspendedToActiveToClosed_ShouldSucceed()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        // PendingReview -> Active
        shop.Approve(ValidReviewerId);
        shop.Status.Should().Be(ShopStatus.Active);

        // Active -> Suspended
        shop.Suspend("Suspension reason");
        shop.Status.Should().Be(ShopStatus.Suspended);

        // Suspended -> Active
        shop.Resume();
        shop.Status.Should().Be(ShopStatus.Active);

        // Active -> Closed
        shop.Close("Closing reason");
        shop.Status.Should().Be(ShopStatus.Closed);
    }

    [Fact]
    public void Workflow_PendingReviewToRejectedToClosed_ShouldSucceed()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);

        // PendingReview -> Rejected
        shop.Reject(ValidReviewerId, "Rejection reason");
        shop.Status.Should().Be(ShopStatus.Rejected);

        // Rejected -> Closed
        shop.Close("Closing reason");
        shop.Status.Should().Be(ShopStatus.Closed);
    }

    [Fact]
    public void Workflow_ActiveToSuspendedToClosed_ShouldSucceed()
    {
        var shop = CreateActiveShop();

        // Active -> Suspended
        shop.Suspend("Suspension reason");
        shop.Status.Should().Be(ShopStatus.Suspended);

        // Suspended -> Closed
        shop.Close("Closing reason");
        shop.Status.Should().Be(ShopStatus.Closed);
    }

    // ==================== Qualification management ====================

    [Fact]
    public void AddQualification_WhenActive_ShouldAddToCollection()
    {
        var shop = CreateActiveShop();
        var qualification = ShopQualification.Create(
            Guid.NewGuid(), ValidShopId, QualificationType.BusinessLicense,
            "LIC123", "https://img.url", DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        shop.AddQualification(qualification);

        shop.Qualifications.Should().ContainSingle()
            .Which.Should().Be(qualification);
    }

    [Fact]
    public void AddQualification_WhenClosed_ShouldThrowShopClosed()
    {
        var shop = CreateClosedShop();
        var qualification = ShopQualification.Create(
            Guid.NewGuid(), ValidShopId, QualificationType.BusinessLicense,
            "LIC123", "https://img.url", DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        var act = () => shop.AddQualification(qualification);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SHOP_CLOSED");
    }

    [Fact]
    public void AddQualification_WithMismatchedShopId_ShouldThrowQualificationShopMismatch()
    {
        var shop = CreateActiveShop();
        var qualification = ShopQualification.Create(
            Guid.NewGuid(), Guid.NewGuid(), QualificationType.BusinessLicense,
            "LIC123", "https://img.url", DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        var act = () => shop.AddQualification(qualification);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_SHOP_MISMATCH");
    }

    [Fact]
    public void AddQualification_WithNullQualification_ShouldThrowArgumentNull()
    {
        var shop = CreateActiveShop();

        var act = () => shop.AddQualification(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetQualification_WhenExists_ShouldReturnQualification()
    {
        var shop = CreateActiveShop();
        var qualId = Guid.NewGuid();
        var qualification = ShopQualification.Create(
            qualId, ValidShopId, QualificationType.BusinessLicense,
            "LIC123", "https://img.url", DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        shop.AddQualification(qualification);

        var result = shop.GetQualification(qualId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(qualId);
    }

    [Fact]
    public void GetQualification_WhenNotExists_ShouldReturnNull()
    {
        var shop = CreateActiveShop();

        var result = shop.GetQualification(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public void ApproveQualification_WhenQualificationExists_ShouldApprove()
    {
        var shop = CreateActiveShop();
        var qualId = Guid.NewGuid();
        var qualification = ShopQualification.Create(
            qualId, ValidShopId, QualificationType.BusinessLicense,
            "LIC123", "https://img.url", DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        shop.AddQualification(qualification);

        shop.ApproveQualification(qualId, ValidReviewerId);

        var approved = shop.GetQualification(qualId);
        approved!.Status.Should().Be(QualificationStatus.Approved);
        approved.ReviewedBy.Should().Be(ValidReviewerId);
    }

    [Fact]
    public void ApproveQualification_WhenQualificationNotExists_ShouldThrowNotFound()
    {
        var shop = CreateActiveShop();

        var act = () => shop.ApproveQualification(Guid.NewGuid(), ValidReviewerId);

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_NOT_FOUND");
        ex.HttpStatusCode.Should().Be(404);
    }

    [Fact]
    public void RejectQualification_WhenQualificationExists_ShouldReject()
    {
        var shop = CreateActiveShop();
        var qualId = Guid.NewGuid();
        var qualification = ShopQualification.Create(
            qualId, ValidShopId, QualificationType.BusinessLicense,
            "LIC123", "https://img.url", DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        shop.AddQualification(qualification);

        shop.RejectQualification(qualId, ValidReviewerId, "Image not clear");

        var rejected = shop.GetQualification(qualId);
        rejected!.Status.Should().Be(QualificationStatus.Rejected);
        rejected.ReviewedBy.Should().Be(ValidReviewerId);
        rejected.RejectReason.Should().Be("Image not clear");
    }

    [Fact]
    public void RejectQualification_WhenQualificationNotExists_ShouldThrowNotFound()
    {
        var shop = CreateActiveShop();

        var act = () => shop.RejectQualification(Guid.NewGuid(), ValidReviewerId, "reason");

        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("QUALIFICATION_NOT_FOUND");
    }

    // ==================== Helper methods ====================

    private static Shop CreateActiveShop()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);
        shop.Approve(ValidReviewerId);
        return shop;
    }

    private static Shop CreateSuspendedShop()
    {
        var shop = CreateActiveShop();
        shop.Suspend(ValidReason);
        return shop;
    }

    private static Shop CreateRejectedShop()
    {
        var shop = Shop.Create(ValidShopId, ValidUserId, ValidName, ValidPhone);
        shop.Reject(ValidReviewerId, ValidReason);
        return shop;
    }

    private static Shop CreateClosedShop()
    {
        var shop = CreateActiveShop();
        shop.Close(ValidReason);
        return shop;
    }
}