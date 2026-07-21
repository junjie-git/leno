using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Events;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.UserAuth.Domain.Tests;

public class UserTests
{
    private readonly Mock<IPasswordHasher> _hasherMock = new();

    public UserTests()
    {
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => $"hashed:{p}");
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string plain, string hash) => hash == $"hashed:{plain}");
    }

    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldCreateActiveUserWithBuyerRole()
    {
        var user = User.Create(
            Guid.NewGuid(), "testuser", "test@test.com", null,
            _hasherMock.Object.Hash("Pass123!"), "Test Nick", null);

        user.Username.Should().Be("testuser");
        user.Email.Should().Be("test@test.com");
        user.Status.Should().Be(AccountStatus.Active);
        user.Roles.Should().ContainSingle(r => r.Value == RoleType.Buyer);
        user.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Create_NoLoginMethod_ShouldThrowException()
    {
        var act = () => User.Create(
            Guid.NewGuid(), "testuser", null, null,
            null, "Test Nick", null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_InvalidUsername_ShouldThrowException()
    {
        var act = () => User.Create(
            Guid.NewGuid(), "ab", "test@test.com", null,
            _hasherMock.Object.Hash("Pass123!"), "Test Nick", null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_InvalidNickname_ShouldThrowException()
    {
        var act = () => User.Create(
            Guid.NewGuid(), "testuser", "test@test.com", null,
            _hasherMock.Object.Hash("Pass123!"), "", null);

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region VerifyPassword

    [Fact]
    public void VerifyPassword_CorrectPassword_ShouldReturnTrue()
    {
        var user = CreateUser();
        user.VerifyPassword("wrong", _hasherMock.Object);
        user.VerifyPassword("wrong", _hasherMock.Object);
        user.FailedLoginCount.Should().Be(2);

        var result = user.VerifyPassword("Pass123!", _hasherMock.Object);

        result.Should().BeTrue();
        user.FailedLoginCount.Should().Be(2);
    }

    [Fact]
    public void RecordLogin_ShouldResetFailedLoginCount()
    {
        var user = CreateUser();
        user.VerifyPassword("wrong", _hasherMock.Object);
        user.FailedLoginCount.Should().Be(1);

        user.RecordLogin();

        user.FailedLoginCount.Should().Be(0);
    }

    [Fact]
    public void VerifyPassword_IncorrectPassword_ShouldIncrementFailedCount()
    {
        var user = CreateUser();

        var result = user.VerifyPassword("wrong", _hasherMock.Object);

        result.Should().BeFalse();
        user.FailedLoginCount.Should().Be(1);
    }

    [Fact]
    public void VerifyPassword_ExceedMaxFailedCount_ShouldLockAccount()
    {
        var user = CreateUser();

        for (int i = 0; i < 5; i++)
            user.VerifyPassword("wrong", _hasherMock.Object);

        user.Status.Should().Be(AccountStatus.Locked);
        user.FailedLoginCount.Should().Be(0);
    }

    [Fact]
    public void VerifyPassword_EmptyHash_ShouldStillInvokeHasherVerifyForTimingEqualization()
    {
        // 纯 OAuth 用户无密码哈希，VerifyPassword 应执行一次 dummy verify 对齐响应时间（P2-4）。
        // 验证 hasher.Verify 被调用，且不递增 FailedLoginCount。
        var info = new ExternalLoginInfo("google", "google-123", "test@gmail.com", "Test User", null);
        var user = User.CreateFromExternal(Guid.NewGuid(), info);
        user.PasswordHash.Should().BeNull();

        var result = user.VerifyPassword("AnyPassword123", _hasherMock.Object);

        result.Should().BeFalse();
        user.FailedLoginCount.Should().Be(0);
        _hasherMock.Verify(h => h.Verify("AnyPassword123", It.Is<string>(s => s.StartsWith("$2a$12$")), Times.Once));
    }

    [Fact]
    public void VerifyPassword_EmptyHash_ShouldReturnFalseWithoutLocking()
    {
        // 纯 OAuth 用户多次调用 VerifyPassword 不应触发锁定（dummy verify 路径不计入失败次数）
        var info = new ExternalLoginInfo("google", "google-123", "test@gmail.com", "Test User", null);
        var user = User.CreateFromExternal(Guid.NewGuid(), info);

        for (int i = 0; i < 10; i++)
        {
            user.VerifyPassword("AnyPassword123", _hasherMock.Object).Should().BeFalse();
        }

        user.Status.Should().Be(AccountStatus.Active);
        user.FailedLoginCount.Should().Be(0);
    }

    [Fact]
    public void VerifyPassword_EmptyPlainPassword_ShouldStillInvokeHasherVerifyForTimingEqualization()
    {
        // 空明文密码也应执行 dummy verify 对齐时序（P2-4）
        var user = CreateUser();

        var result = user.VerifyPassword("", _hasherMock.Object);

        result.Should().BeFalse();
        _hasherMock.Verify(h => h.Verify("\x00", It.Is<string>(s => s.StartsWith("$2a$12$")), Times.Once));
    }

    [Fact]
    public void VerifyPassword_NullPlainPassword_ShouldStillInvokeHasherVerifyForTimingEqualization()
    {
        var user = CreateUser();

        var result = user.VerifyPassword(null!, _hasherMock.Object);

        result.Should().BeFalse();
        _hasherMock.Verify(h => h.Verify("\x00", It.Is<string>(s => s.StartsWith("$2a$12$")), Times.Once));
    }

    #endregion

    #region ChangePassword

    [Fact]
    public void ChangePassword_ValidOldPassword_ShouldUpdateHash()
    {
        var user = CreateUser();
        user.ChangePassword("Pass123!", "NewPass456!", _hasherMock.Object);

        user.PasswordHash.Should().Be("hashed:NewPass456!");
        user.DomainEvents.Should().Contain(e => e is UserPasswordChangedEvent);
    }

    [Fact]
    public void ChangePassword_WrongOldPassword_ShouldThrowException()
    {
        var user = CreateUser();

        var act = () => user.ChangePassword("wrong", "NewPass456!", _hasherMock.Object);

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region Lock / Unlock

    [Fact]
    public void Lock_ValidUser_ShouldSetLockedStatus()
    {
        var user = CreateUser();
        user.Lock("suspicious activity", TimeSpan.FromHours(1));

        user.Status.Should().Be(AccountStatus.Locked);
        user.LockedUntil.Should().NotBeNull();
    }

    [Fact]
    public void Lock_DisabledUser_ShouldThrowException()
    {
        var user = CreateUser();
        user.Disable("test", Guid.NewGuid());

        var act = () => user.Lock("test", TimeSpan.FromHours(1));

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Unlock_LockedUser_ShouldReturnToActive()
    {
        var user = CreateUser();
        user.Lock("test", TimeSpan.FromHours(1));
        user.Unlock();

        user.Status.Should().Be(AccountStatus.Active);
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    #endregion

    #region Disable / Activate

    [Fact]
    public void Disable_ActiveUser_ShouldSetDisabledStatus()
    {
        var user = CreateUser();
        user.Disable("violation", Guid.NewGuid());

        user.Status.Should().Be(AccountStatus.Disabled);
    }

    [Fact]
    public void Activate_DisabledUser_ShouldReturnToActive()
    {
        var user = CreateUser();
        user.Disable("test", Guid.NewGuid());
        user.Activate();

        user.Status.Should().Be(AccountStatus.Active);
    }

    #endregion

    #region AssignRole / RevokeRole

    [Fact]
    public void AssignRole_NewRole_ShouldAddRole()
    {
        var user = CreateUser();
        user.AssignRole(RoleType.Seller, null);

        user.Roles.Should().Contain(r => r.Value == RoleType.Seller);
        user.Roles.Should().Contain(r => r.Value == RoleType.Buyer);
    }

    [Fact]
    public void AssignRole_DuplicateRole_ShouldNotAddDuplicate()
    {
        var user = CreateUser();
        user.AssignRole(RoleType.Seller, null);
        user.AssignRole(RoleType.Seller, null);

        user.Roles.Count(r => r.Value == RoleType.Seller).Should().Be(1);
    }

    [Fact]
    public void RevokeRole_LastRole_ShouldThrowException()
    {
        var user = CreateUser();
        var act = () => user.RevokeRole(RoleType.Buyer, null);

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region CanLogin

    [Fact]
    public void CanLogin_ActiveUser_ShouldReturnTrue()
    {
        var user = CreateUser();
        user.CanLogin().Should().BeTrue();
    }

    [Fact]
    public void CanLogin_DisabledUser_ShouldReturnFalse()
    {
        var user = CreateUser();
        user.Disable("test", Guid.NewGuid());
        user.CanLogin().Should().BeFalse();
    }

    [Fact]
    public void CanLogin_LockedUser_ShouldReturnFalse()
    {
        var user = CreateUser();
        user.Lock("test", TimeSpan.FromHours(1));
        user.CanLogin().Should().BeFalse();
    }

    #endregion

    #region Profile

    [Fact]
    public void UpdateProfile_ValidInput_ShouldUpdateNickname()
    {
        var user = CreateUser();
        user.UpdateProfile("New Nick", "https://example.com/avatar.png");

        user.Nickname.Should().Be("New Nick");
        user.AvatarUrl.Should().Be("https://example.com/avatar.png");
    }

    #endregion

    #region CreateFromExternal

    [Fact]
    public void CreateFromExternal_ValidInfo_ShouldCreateActiveUserWithBuyerRole()
    {
        var info = new ExternalLoginInfo("google", "google-123", "test@gmail.com", "Test User", "https://example.com/avatar.png");

        var user = User.CreateFromExternal(Guid.NewGuid(), info);

        user.Username.Should().NotBeNullOrEmpty();
        user.Email.Should().Be("test@gmail.com");
        user.Nickname.Should().Be("Test User");
        user.AvatarUrl.Should().Be("https://example.com/avatar.png");
        user.PasswordHash.Should().BeNull();
        user.PhoneNumber.Should().BeNull();
        user.Status.Should().Be(AccountStatus.Active);
        user.Roles.Should().ContainSingle(r => r.Value == RoleType.Buyer);
        user.ExternalLogins.Should().HaveCount(1);
        user.ExternalLogins.First().Provider.Should().Be("google");
        user.ExternalLogins.First().ProviderUserId.Should().Be("google-123");
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.Should().Contain(e => e is UserRegisteredDomainEvent);
    }

    [Fact]
    public void CreateFromExternal_EmptyId_ShouldThrowException()
    {
        var info = new ExternalLoginInfo("google", "google-123", "test@gmail.com", "Test User", null);

        var act = () => User.CreateFromExternal(Guid.Empty, info);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void CreateFromExternal_NullInfo_ShouldThrowException()
    {
        var act = () => User.CreateFromExternal(Guid.NewGuid(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateFromExternal_EmptyName_ShouldUseEmailPrefix()
    {
        var info = new ExternalLoginInfo("google", "google-123", "test@gmail.com", " ", null);

        var user = User.CreateFromExternal(Guid.NewGuid(), info);

        user.Nickname.Should().Be("test");
    }

    #endregion

    #region LinkExternalLogin

    [Fact]
    public void LinkExternalLogin_ValidProvider_ShouldAddExternalLogin()
    {
        var user = CreateUser();

        user.LinkExternalLogin("google", "google-123", "test@gmail.com", "Test User", null);

        user.ExternalLogins.Should().HaveCount(1);
        user.ExternalLogins.First().Provider.Should().Be("google");
        user.ExternalLogins.First().ProviderUserId.Should().Be("google-123");
        user.DomainEvents.Should().Contain(e => e is ExternalLoginLinkedEvent);
    }

    [Fact]
    public void LinkExternalLogin_DuplicateProvider_ShouldThrowException()
    {
        var user = CreateUser();
        user.LinkExternalLogin("google", "google-123", "test@gmail.com", "Test User", null);

        var act = () => user.LinkExternalLogin("google", "google-456", "other@gmail.com", "Other", null);

        act.Should().Throw<UserAuthDomainException>()
            .Where(ex => ex.Message.Contains("已绑定"));
    }

    [Fact]
    public void LinkExternalLogin_MultipleProviders_ShouldSucceed()
    {
        var user = CreateUser();
        user.LinkExternalLogin("google", "google-123", "test@gmail.com", "Test User", null);
        user.LinkExternalLogin("wechat", "wechat-456", "test@wechat.local", "WeChat User", null);

        user.ExternalLogins.Should().HaveCount(2);
    }

    [Fact]
    public void LinkExternalLogin_EmptyProvider_ShouldThrowException()
    {
        var user = CreateUser();

        var act = () => user.LinkExternalLogin("", "id", "e@e.com", "Name", null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void LinkExternalLogin_EmptyProviderUserId_ShouldThrowException()
    {
        var user = CreateUser();

        var act = () => user.LinkExternalLogin("google", "", "e@e.com", "Name", null);

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region UnlinkExternalLogin

    [Fact]
    public void UnlinkExternalLogin_ExistingProvider_ShouldRemoveExternalLogin()
    {
        var user = CreateUser();
        user.LinkExternalLogin("google", "google-123", "test@gmail.com", "Test User", null);

        user.UnlinkExternalLogin("google");

        user.ExternalLogins.Should().BeEmpty();
        user.DomainEvents.Should().Contain(e => e is ExternalLoginUnlinkedEvent);
    }

    [Fact]
    public void UnlinkExternalLogin_NonExistingProvider_ShouldNotThrow()
    {
        var user = CreateUser();

        var act = () => user.UnlinkExternalLogin("google");

        act.Should().NotThrow();
        user.ExternalLogins.Should().BeEmpty();
    }

    [Fact]
    public void UnlinkExternalLogin_LastLoginForOAuthUser_ShouldThrowException()
    {
        var info = new ExternalLoginInfo("google", "google-123", "test@gmail.com", "Test User", null);
        var user = User.CreateFromExternal(Guid.NewGuid(), info);

        var act = () => user.UnlinkExternalLogin("google");

        act.Should().Throw<UserAuthDomainException>()
            .Where(ex => ex.Message.Contains("至少保留一个"));
    }

    [Fact]
    public void UnlinkExternalLogin_OAuthUserWithMultipleProviders_ShouldSucceed()
    {
        var info = new ExternalLoginInfo("google", "google-123", "test@gmail.com", "Test User", null);
        var user = User.CreateFromExternal(Guid.NewGuid(), info);
        user.LinkExternalLogin("wechat", "wechat-456", "test@wechat.local", "WX", null);

        user.UnlinkExternalLogin("google");

        user.ExternalLogins.Should().HaveCount(1);
        user.ExternalLogins.First().Provider.Should().Be("wechat");
    }

    [Fact]
    public void UnlinkExternalLogin_PasswordUser_ShouldAllowRemovingAll()
    {
        var user = CreateUser();
        user.LinkExternalLogin("google", "google-123", "test@gmail.com", "Test User", null);

        user.UnlinkExternalLogin("google");

        user.ExternalLogins.Should().BeEmpty();
    }

    #endregion

    #region Rename

    [Fact]
    public void Rename_Should_Update_Username_With_Validation()
    {
        // Arrange
        var user = User.Create(
            Guid.NewGuid(),
            "oldname",
            "user@example.com",
            "+8613800138000",
            _hasherMock.Object.Hash("Password123"),
            "Nick");

        // Act
        user.Rename("newname");

        // Assert
        user.Username.Should().Be("newname");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("this_username_is_way_too_long_for_validation_xxxxxxx")]
    [InlineData("invalid chars!")]
    public void Rename_Should_Throw_When_Username_Invalid(string invalid)
    {
        var user = User.Create(
            Guid.NewGuid(),
            "oldname",
            "user@example.com",
            "+8613800138000",
            _hasherMock.Object.Hash("Password123"),
            "Nick");

        var act = () => user.Rename(invalid);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Rename_Should_Trim_Username()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "oldname",
            "user@example.com",
            "+8613800138000",
            _hasherMock.Object.Hash("Password123"),
            "Nick");

        user.Rename("  newname  ");

        user.Username.Should().Be("newname");
    }

    #endregion

    private User CreateUser(string? passwordHash = null)
    {
        return User.Create(
            Guid.NewGuid(), "testuser", "test@test.com", null,
            passwordHash ?? _hasherMock.Object.Hash("Pass123!"),
            "Test Nick", null);
    }
}