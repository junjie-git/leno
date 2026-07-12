using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.ValueObjects;

namespace Leno.SellerShop.Domain.Tests;

public class SellerProfileTests
{
    private static readonly Guid ValidSellerId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private static readonly Guid ValidReviewerId = Guid.NewGuid();
    private const string ValidRealName = "John Doe";
    private const string ValidIdCard = "110101199001011234";
    private const string ValidLicenseNo = "LIC123456789";
    private const string ValidBankAccount = "6222021234567890123";
    private const string ValidReason = "Valid rejection reason";

    // ==================== Create factory ====================

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            ValidIdCard, ValidLicenseNo, ValidBankAccount);

        profile.Id.Should().Be(ValidSellerId);
        profile.UserId.Should().Be(ValidUserId);
        profile.RealName.Should().Be(ValidRealName);
        profile.IdCard.Should().Be(ValidIdCard);
        profile.BusinessLicenseNo.Should().Be(ValidLicenseNo);
        profile.BankAccount.Should().Be(ValidBankAccount);
        profile.Status.Should().Be(SellerStatus.Draft);
    }

    [Fact]
    public void Create_WithNullOptionalFields_ShouldSetNulls()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        profile.IdCard.Should().BeNull();
        profile.BusinessLicenseNo.Should().BeNull();
        profile.BankAccount.Should().BeNull();
    }

    [Fact]
    public void Create_WithWhitespaceOptionalFields_ShouldTrimToNull()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            "   ", "   ", "   ");

        profile.IdCard.Should().BeNull();
        profile.BusinessLicenseNo.Should().BeNull();
        profile.BankAccount.Should().BeNull();
    }

    [Fact]
    public void Create_WithMaxRealNameLength_ShouldSucceed()
    {
        var name = new string('N', 32);
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, name);

        profile.RealName.Should().Be(name);
    }

    [Fact]
    public void Create_WithMaxIdCardLength_ShouldSucceed()
    {
        var idCard = new string('1', 18);
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName, idCard: idCard);

        profile.IdCard.Should().Be(idCard);
    }

    [Fact]
    public void Create_WithMaxLicenseNoLength_ShouldSucceed()
    {
        var license = new string('L', 32);
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName, businessLicenseNo: license);

        profile.BusinessLicenseNo.Should().Be(license);
    }

    [Fact]
    public void Create_WithMaxBankAccountLength_ShouldSucceed()
    {
        var account = new string('6', 64);
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName, bankAccount: account);

        profile.BankAccount.Should().Be(account);
    }

    // ==================== Create validation guards ====================

    [Fact]
    public void Create_WithEmptySellerId_ShouldThrowSellerIdEmpty()
    {
        var act = () => SellerProfile.Create(Guid.Empty, ValidUserId, ValidRealName);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowSellerUserEmpty()
    {
        var act = () => SellerProfile.Create(ValidSellerId, Guid.Empty, ValidRealName);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_USER_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyRealName_ShouldThrowSellerRealNameEmpty()
    {
        var act = () => SellerProfile.Create(ValidSellerId, ValidUserId, "");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_REAL_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithWhitespaceRealName_ShouldThrowSellerRealNameEmpty()
    {
        var act = () => SellerProfile.Create(ValidSellerId, ValidUserId, "   ");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_REAL_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithTooLongRealName_ShouldThrowSellerRealNameLength()
    {
        var act = () => SellerProfile.Create(ValidSellerId, ValidUserId, new string('N', 33));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_REAL_NAME_LENGTH");
    }

    [Fact]
    public void Create_WithTooLongIdCard_ShouldThrowSellerIdCardLength()
    {
        var act = () => SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            idCard: new string('1', 19));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_ID_CARD_LENGTH");
    }

    [Fact]
    public void Create_WithTooLongLicenseNo_ShouldThrowSellerLicenseLength()
    {
        var act = () => SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            businessLicenseNo: new string('L', 33));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_LICENSE_LENGTH");
    }

    [Fact]
    public void Create_WithTooLongBankAccount_ShouldThrowSellerBankAccountLength()
    {
        var act = () => SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            bankAccount: new string('6', 65));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_BANK_ACCOUNT_LENGTH");
    }

    [Fact]
    public void Create_ShouldTrimFieldValues()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, "  " + ValidRealName + "  ",
            "  " + ValidIdCard + "  ");

        profile.RealName.Should().Be(ValidRealName);
        profile.IdCard.Should().Be(ValidIdCard);
    }

    // ==================== Update ====================

    [Fact]
    public void Update_WhenDraft_ShouldUpdateFields()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        profile.Update("New Name", "new-id-card", "new-license", "new-account");

        profile.RealName.Should().Be("New Name");
        profile.IdCard.Should().Be("new-id-card");
        profile.BusinessLicenseNo.Should().Be("new-license");
        profile.BankAccount.Should().Be("new-account");
    }

    [Fact]
    public void Update_WhenRejected_ShouldUpdateFields()
    {
        var profile = CreateRejectedProfile();

        profile.Update("New Name", "new-id-card", "new-license", "new-account");

        profile.RealName.Should().Be("New Name");
        profile.IdCard.Should().Be("new-id-card");
        profile.BusinessLicenseNo.Should().Be("new-license");
    }

    [Fact]
    public void Update_WhenPendingReview_ShouldUpdateFields()
    {
        var profile = CreatePendingReviewProfile();

        profile.Update("New Name", null, null, null);

        profile.RealName.Should().Be("New Name");
    }

    [Fact]
    public void Update_WithNullOptionalFields_ShouldSetNulls()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            ValidIdCard, ValidLicenseNo, ValidBankAccount);

        profile.Update("New Name", null, null, null);

        profile.IdCard.Should().BeNull();
        profile.BusinessLicenseNo.Should().BeNull();
        profile.BankAccount.Should().BeNull();
    }

    [Fact]
    public void Update_WithWhitespaceOptionalFields_ShouldTrimToNull()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            ValidIdCard, ValidLicenseNo, ValidBankAccount);

        profile.Update("New Name", "   ", "   ", "   ");

        profile.IdCard.Should().BeNull();
        profile.BusinessLicenseNo.Should().BeNull();
        profile.BankAccount.Should().BeNull();
    }

    [Fact]
    public void Update_WhenApproved_ShouldThrowSellerApproved()
    {
        var profile = CreateApprovedProfile();

        var act = () => profile.Update("New Name", null, null, null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_APPROVED");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void Update_WithEmptyRealName_ShouldThrowSellerRealNameEmpty()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        var act = () => profile.Update("", null, null, null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_REAL_NAME_EMPTY");
    }

    [Fact]
    public void Update_WithTooLongRealName_ShouldThrowSellerRealNameLength()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        var act = () => profile.Update(new string('N', 33), null, null, null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_REAL_NAME_LENGTH");
    }

    [Fact]
    public void Update_WithTooLongIdCard_ShouldThrowSellerIdCardLength()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        var act = () => profile.Update("Valid Name", new string('1', 19), null, null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_ID_CARD_LENGTH");
    }

    [Fact]
    public void Update_WithTooLongLicenseNo_ShouldThrowSellerLicenseLength()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        var act = () => profile.Update("Valid Name", null, new string('L', 33), null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_LICENSE_LENGTH");
    }

    [Fact]
    public void Update_WithTooLongBankAccount_ShouldThrowSellerBankAccountLength()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        var act = () => profile.Update("Valid Name", null, null, new string('6', 65));
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_BANK_ACCOUNT_LENGTH");
    }

    // ==================== SubmitForVerification ====================

    [Fact]
    public void SubmitForVerification_WhenDraftWithIdCard_ShouldSetPendingReview()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            idCard: ValidIdCard);

        profile.SubmitForVerification();

        profile.Status.Should().Be(SellerStatus.PendingReview);
        profile.StatusReason.Should().BeNull();
    }

    [Fact]
    public void SubmitForVerification_WhenDraftWithLicenseNo_ShouldSetPendingReview()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            businessLicenseNo: ValidLicenseNo);

        profile.SubmitForVerification();

        profile.Status.Should().Be(SellerStatus.PendingReview);
    }

    [Fact]
    public void SubmitForVerification_WhenDraftWithBoth_ShouldSetPendingReview()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            ValidIdCard, ValidLicenseNo);

        profile.SubmitForVerification();

        profile.Status.Should().Be(SellerStatus.PendingReview);
    }

    [Fact]
    public void SubmitForVerification_WhenRejected_ShouldSetPendingReview()
    {
        var profile = CreateRejectedProfile();

        profile.SubmitForVerification();

        profile.Status.Should().Be(SellerStatus.PendingReview);
        profile.StatusReason.Should().BeNull();
    }

    [Fact]
    public void SubmitForVerification_WhenPendingReview_ShouldThrowInvalidTransition()
    {
        var profile = CreatePendingReviewProfile();

        var act = () => profile.SubmitForVerification();
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_INVALID_TRANSITION");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void SubmitForVerification_WhenApproved_ShouldThrowInvalidTransition()
    {
        var profile = CreateApprovedProfile();

        var act = () => profile.SubmitForVerification();
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_INVALID_TRANSITION");
    }

    [Fact]
    public void SubmitForVerification_WithoutQualification_ShouldThrowNoQualification()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        var act = () => profile.SubmitForVerification();
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_NO_QUALIFICATION");
    }

    [Fact]
    public void SubmitForVerification_WithWhitespaceOnlyQualification_ShouldThrowNoQualification()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            idCard: "   ");

        var act = () => profile.SubmitForVerification();
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_NO_QUALIFICATION");
    }

    // ==================== Approve ====================

    [Fact]
    public void Approve_WhenPendingReview_ShouldSetApproved()
    {
        var profile = CreatePendingReviewProfile();

        profile.Approve(ValidReviewerId);

        profile.Status.Should().Be(SellerStatus.Approved);
        profile.ReviewedBy.Should().Be(ValidReviewerId);
        profile.StatusReason.Should().BeNull();
    }

    [Fact]
    public void Approve_WhenDraft_ShouldThrowInvalidTransition()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        var act = () => profile.Approve(ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_INVALID_TRANSITION");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void Approve_WhenApproved_ShouldThrowInvalidTransition()
    {
        var profile = CreateApprovedProfile();

        var act = () => profile.Approve(ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_INVALID_TRANSITION");
    }

    [Fact]
    public void Approve_WhenRejected_ShouldThrowInvalidTransition()
    {
        var profile = CreateRejectedProfile();

        var act = () => profile.Approve(ValidReviewerId);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_INVALID_TRANSITION");
    }

    [Fact]
    public void Approve_WithEmptyReviewer_ShouldThrowSellerReviewerEmpty()
    {
        var profile = CreatePendingReviewProfile();

        var act = () => profile.Approve(Guid.Empty);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_REVIEWER_EMPTY");
    }

    // ==================== Reject ====================

    [Fact]
    public void Reject_WhenPendingReview_ShouldSetRejected()
    {
        var profile = CreatePendingReviewProfile();

        profile.Reject(ValidReviewerId, ValidReason);

        profile.Status.Should().Be(SellerStatus.Rejected);
        profile.ReviewedBy.Should().Be(ValidReviewerId);
        profile.StatusReason.Should().Be(ValidReason);
    }

    [Fact]
    public void Reject_WhenDraft_ShouldThrowInvalidTransition()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName);

        var act = () => profile.Reject(ValidReviewerId, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_INVALID_TRANSITION");
        ex.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void Reject_WhenApproved_ShouldThrowInvalidTransition()
    {
        var profile = CreateApprovedProfile();

        var act = () => profile.Reject(ValidReviewerId, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_INVALID_TRANSITION");
    }

    [Fact]
    public void Reject_WhenRejected_ShouldThrowInvalidTransition()
    {
        var profile = CreateRejectedProfile();

        var act = () => profile.Reject(ValidReviewerId, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_INVALID_TRANSITION");
    }

    [Fact]
    public void Reject_WithEmptyReviewer_ShouldThrowSellerReviewerEmpty()
    {
        var profile = CreatePendingReviewProfile();

        var act = () => profile.Reject(Guid.Empty, ValidReason);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_REVIEWER_EMPTY");
    }

    [Fact]
    public void Reject_WithEmptyReason_ShouldThrowSellerReasonEmpty()
    {
        var profile = CreatePendingReviewProfile();

        var act = () => profile.Reject(ValidReviewerId, "");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_REASON_EMPTY");
    }

    [Fact]
    public void Reject_WithWhitespaceReason_ShouldThrowSellerReasonEmpty()
    {
        var profile = CreatePendingReviewProfile();

        var act = () => profile.Reject(ValidReviewerId, "   ");
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_REASON_EMPTY");
    }

    // ==================== Full state machine workflows ====================

    [Fact]
    public void FullWorkflow_DraftToPendingReviewToApproved_ShouldSucceed()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            idCard: ValidIdCard);

        // Draft -> PendingReview
        profile.SubmitForVerification();
        profile.Status.Should().Be(SellerStatus.PendingReview);

        // PendingReview -> Approved
        profile.Approve(ValidReviewerId);
        profile.Status.Should().Be(SellerStatus.Approved);
    }

    [Fact]
    public void FullWorkflow_DraftToPendingReviewToRejected_ShouldSucceed()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            idCard: ValidIdCard);

        // Draft -> PendingReview
        profile.SubmitForVerification();
        profile.Status.Should().Be(SellerStatus.PendingReview);

        // PendingReview -> Rejected
        profile.Reject(ValidReviewerId, ValidReason);
        profile.Status.Should().Be(SellerStatus.Rejected);
    }

    [Fact]
    public void FullWorkflow_RejectedToPendingReviewToApproved_ShouldSucceed()
    {
        var profile = CreateRejectedProfile();

        // Rejected -> PendingReview (resubmit)
        profile.SubmitForVerification();
        profile.Status.Should().Be(SellerStatus.PendingReview);

        // PendingReview -> Approved
        profile.Approve(ValidReviewerId);
        profile.Status.Should().Be(SellerStatus.Approved);
    }

    [Fact]
    public void Update_AfterApproved_ShouldBeBlocked()
    {
        var profile = CreateApprovedProfile();

        var act = () => profile.Update("New Name", null, null, null);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("SELLER_APPROVED");
    }

    // ==================== Helper methods ====================

    private static SellerProfile CreatePendingReviewProfile()
    {
        var profile = SellerProfile.Create(ValidSellerId, ValidUserId, ValidRealName,
            idCard: ValidIdCard);
        profile.SubmitForVerification();
        return profile;
    }

    private static SellerProfile CreateApprovedProfile()
    {
        var profile = CreatePendingReviewProfile();
        profile.Approve(ValidReviewerId);
        return profile;
    }

    private static SellerProfile CreateRejectedProfile()
    {
        var profile = CreatePendingReviewProfile();
        profile.Reject(ValidReviewerId, ValidReason);
        return profile;
    }
}