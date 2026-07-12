using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Events;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class SystemAnnouncementTests
{
    private static readonly Guid ValidAnnouncementId = Guid.NewGuid();
    private const string ValidTitle = "System Maintenance Notice";
    private const string ValidContent = "The system will be under maintenance on Sunday.";
    private static readonly DateTime FutureDate = DateTime.UtcNow.AddDays(1);

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var expireAt = DateTime.UtcNow.AddDays(7);

        var announcement = SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt);

        announcement.AnnouncementId.Should().Be(ValidAnnouncementId);
        announcement.Id.Should().Be(ValidAnnouncementId);
        announcement.Title.Should().Be(ValidTitle);
        announcement.Content.Should().Be(ValidContent);
        announcement.Type.Should().Be(AnnouncementType.System);
        announcement.TargetAudience.Should().Be(AnnouncementTargetAudience.All);
        announcement.PublishAt.Should().Be(FutureDate);
        announcement.ExpireAt.Should().Be(expireAt);
        announcement.Status.Should().Be(AnnouncementStatus.Draft);
    }

    [Fact]
    public void Create_WithMinimalParameters_ShouldSetDefaults()
    {
        var announcement = SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, ValidContent,
            AnnouncementType.Maintenance, AnnouncementTargetAudience.Buyers,
            publishAt: null, expireAt: null);

        announcement.PublishAt.Should().BeNull();
        announcement.ExpireAt.Should().BeNull();
        announcement.Status.Should().Be(AnnouncementStatus.Draft);
    }

    [Fact]
    public void Create_WithPublishAtSlightlyInFuture_ShouldSucceed()
    {
        var slightlyFuture = DateTime.UtcNow.AddMilliseconds(100);

        var announcement = SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, ValidContent,
            AnnouncementType.Promotion, AnnouncementTargetAudience.Sellers,
            slightlyFuture, expireAt: null);

        announcement.PublishAt.Should().Be(slightlyFuture);
    }

    [Fact]
    public void Create_ShouldTrimTitle()
    {
        var announcement = SystemAnnouncement.Create(
            ValidAnnouncementId, "  " + ValidTitle + "  ", ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        announcement.Title.Should().Be(ValidTitle);
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyAnnouncementId_ShouldThrowAnnouncementIdEmpty()
    {
        var act = () => SystemAnnouncement.Create(
            Guid.Empty, ValidTitle, ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullTitle_ShouldThrowAnnouncementTitleEmpty()
    {
        var act = () => SystemAnnouncement.Create(
            ValidAnnouncementId, null!, ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_TITLE_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyTitle_ShouldThrowAnnouncementTitleEmpty()
    {
        var act = () => SystemAnnouncement.Create(
            ValidAnnouncementId, "", ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_TITLE_EMPTY");
    }

    [Fact]
    public void Create_WithTitleTooLong_ShouldThrowAnnouncementTitleLength()
    {
        var longTitle = new string('t', 201);

        var act = () => SystemAnnouncement.Create(
            ValidAnnouncementId, longTitle, ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_TITLE_LENGTH");
    }

    [Fact]
    public void Create_WithTitleAtMaxLength_ShouldSucceed()
    {
        var title = new string('t', 200);

        var announcement = SystemAnnouncement.Create(
            ValidAnnouncementId, title, ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        announcement.Title.Should().Be(title);
    }

    [Fact]
    public void Create_WithNullContent_ShouldThrowAnnouncementContentEmpty()
    {
        var act = () => SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, null!,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_CONTENT_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyContent_ShouldThrowAnnouncementContentEmpty()
    {
        var act = () => SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, "",
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_CONTENT_EMPTY");
    }

    [Fact]
    public void Create_WithContentTooLong_ShouldThrowAnnouncementContentLength()
    {
        var longContent = new string('c', 4001);

        var act = () => SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, longContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_CONTENT_LENGTH");
    }

    [Fact]
    public void Create_WithContentAtMaxLength_ShouldSucceed()
    {
        var content = new string('c', 4000);

        var announcement = SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, content,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        announcement.Content.Should().Be(content);
    }

    [Fact]
    public void Create_WithInvalidType_ShouldThrowAnnouncementTypeInvalid()
    {
        var invalidType = (AnnouncementType)999;

        var act = () => SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, ValidContent,
            invalidType, AnnouncementTargetAudience.All,
            FutureDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_TYPE_INVALID");
    }

    [Fact]
    public void Create_WithInvalidAudience_ShouldThrowAnnouncementAudienceInvalid()
    {
        var invalidAudience = (AnnouncementTargetAudience)999;

        var act = () => SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, ValidContent,
            AnnouncementType.System, invalidAudience,
            FutureDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_AUDIENCE_INVALID");
    }

    [Fact]
    public void Create_WithPastPublishAt_ShouldThrowAnnouncementPublishAtPast()
    {
        var pastDate = DateTime.UtcNow.AddMinutes(-1);

        var act = () => SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            pastDate, expireAt: null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_PUBLISH_AT_PAST");
    }

    #endregion

    #region Publish

    [Fact]
    public void Publish_FromDraft_ShouldTransitionToPublished()
    {
        var announcement = CreateDraftAnnouncement();

        announcement.Publish();

        announcement.Status.Should().Be(AnnouncementStatus.Published);
    }

    [Fact]
    public void Publish_WithNullPublishAt_ShouldSetPublishAtToNow()
    {
        var announcement = CreateDraftAnnouncement(publishAt: null);
        var before = DateTime.UtcNow;

        announcement.Publish();

        announcement.PublishAt.Should().NotBeNull();
        announcement.PublishAt!.Value.Should().BeCloseTo(before, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Publish_WithExistingPublishAt_ShouldNotOverridePublishAt()
    {
        var announcement = CreateDraftAnnouncement(publishAt: FutureDate);

        announcement.Publish();

        announcement.PublishAt.Should().Be(FutureDate);
    }

    [Fact]
    public void Publish_ShouldRaiseAnnouncementPublishedEvent()
    {
        var announcement = CreateDraftAnnouncement();

        announcement.Publish();

        announcement.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AnnouncementPublishedEvent>();
        var evt = (AnnouncementPublishedEvent)announcement.DomainEvents.First();
        evt.AnnouncementId.Should().Be(announcement.Id);
        evt.Title.Should().Be(ValidTitle);
        evt.Type.Should().Be((int)AnnouncementType.System);
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_ShouldThrowAnnouncementAlreadyPublished()
    {
        var announcement = CreateDraftAnnouncement();
        announcement.Publish();

        var act = () => announcement.Publish();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_ALREADY_PUBLISHED");
    }

    [Fact]
    public void Publish_WhenExpired_ShouldThrowAnnouncementExpired()
    {
        var announcement = CreateDraftAnnouncement();
        // Use reflection to set status to Expired since there's no public method
        typeof(SystemAnnouncement)
            .GetProperty(nameof(SystemAnnouncement.Status))!
            .SetValue(announcement, AnnouncementStatus.Expired);

        var act = () => announcement.Publish();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_EXPIRED");
    }

    #endregion

    #region Unpublish

    [Fact]
    public void Unpublish_FromPublished_ShouldTransitionToDraft()
    {
        var announcement = CreateDraftAnnouncement();
        announcement.Publish();

        announcement.Unpublish();

        announcement.Status.Should().Be(AnnouncementStatus.Draft);
    }

    [Fact]
    public void Unpublish_WhenNotPublished_ShouldThrowAnnouncementNotPublished()
    {
        var announcement = CreateDraftAnnouncement();

        var act = () => announcement.Unpublish();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_NOT_PUBLISHED");
    }

    [Fact]
    public void Unpublish_WhenExpired_ShouldThrowAnnouncementNotPublished()
    {
        var announcement = CreateDraftAnnouncement();
        typeof(SystemAnnouncement)
            .GetProperty(nameof(SystemAnnouncement.Status))!
            .SetValue(announcement, AnnouncementStatus.Expired);

        var act = () => announcement.Unpublish();

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_NOT_PUBLISHED");
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WhenDraft_ShouldUpdateAllProperties()
    {
        var announcement = CreateDraftAnnouncement();
        var newPublishAt = DateTime.UtcNow.AddDays(5);
        var newExpireAt = DateTime.UtcNow.AddDays(10);

        announcement.Update(
            "Updated Title", "Updated Content",
            AnnouncementType.Maintenance, AnnouncementTargetAudience.Operators,
            newPublishAt, newExpireAt);

        announcement.Title.Should().Be("Updated Title");
        announcement.Content.Should().Be("Updated Content");
        announcement.Type.Should().Be(AnnouncementType.Maintenance);
        announcement.TargetAudience.Should().Be(AnnouncementTargetAudience.Operators);
        announcement.PublishAt.Should().Be(newPublishAt);
        announcement.ExpireAt.Should().Be(newExpireAt);
        announcement.Status.Should().Be(AnnouncementStatus.Draft);
    }

    [Fact]
    public void Update_WhenPublished_ShouldThrowAnnouncementNotDraft()
    {
        var announcement = CreateDraftAnnouncement();
        announcement.Publish();

        var act = () => announcement.Update(
            "Updated Title", "Updated Content",
            AnnouncementType.Maintenance, AnnouncementTargetAudience.Operators,
            FutureDate, null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_NOT_DRAFT");
    }

    [Fact]
    public void Update_WhenExpired_ShouldThrowAnnouncementNotDraft()
    {
        var announcement = CreateDraftAnnouncement();
        typeof(SystemAnnouncement)
            .GetProperty(nameof(SystemAnnouncement.Status))!
            .SetValue(announcement, AnnouncementStatus.Expired);

        var act = () => announcement.Update(
            "Updated Title", "Updated Content",
            AnnouncementType.Maintenance, AnnouncementTargetAudience.Operators,
            FutureDate, null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_NOT_DRAFT");
    }

    [Fact]
    public void Update_WithEmptyTitle_ShouldThrowAnnouncementTitleEmpty()
    {
        var announcement = CreateDraftAnnouncement();

        var act = () => announcement.Update(
            "", "Updated Content",
            AnnouncementType.System, AnnouncementTargetAudience.All,
            FutureDate, null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_TITLE_EMPTY");
    }

    [Fact]
    public void Update_WithPastPublishAt_ShouldThrowAnnouncementPublishAtPast()
    {
        var announcement = CreateDraftAnnouncement();
        var pastDate = DateTime.UtcNow.AddMinutes(-1);

        var act = () => announcement.Update(
            ValidTitle, ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            pastDate, null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("ANNOUNCEMENT_PUBLISH_AT_PAST");
    }

    #endregion

    #region State Transitions

    [Fact]
    public void StateMachine_PublishThenUnpublish_ShouldReturnToDraft()
    {
        var announcement = CreateDraftAnnouncement();
        announcement.Status.Should().Be(AnnouncementStatus.Draft);

        announcement.Publish();
        announcement.Status.Should().Be(AnnouncementStatus.Published);

        announcement.Unpublish();
        announcement.Status.Should().Be(AnnouncementStatus.Draft);
    }

    [Fact]
    public void StateMachine_UnpublishThenPublish_ShouldWork()
    {
        var announcement = CreateDraftAnnouncement();
        announcement.Publish();
        announcement.Unpublish();
        announcement.ClearDomainEvents();

        announcement.Publish();

        announcement.Status.Should().Be(AnnouncementStatus.Published);
        announcement.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AnnouncementPublishedEvent>();
    }

    #endregion

    private static SystemAnnouncement CreateDraftAnnouncement(DateTime? publishAt = null)
    {
        return SystemAnnouncement.Create(
            ValidAnnouncementId, ValidTitle, ValidContent,
            AnnouncementType.System, AnnouncementTargetAudience.All,
            publishAt, expireAt: null);
    }
}