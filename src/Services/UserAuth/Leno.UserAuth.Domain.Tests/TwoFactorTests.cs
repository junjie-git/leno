using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Events;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Moq;

namespace Leno.UserAuth.Domain.Tests;

public class TwoFactorTests
{
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<ITokenVerifier> _tokenVerifierMock = new();

    public TwoFactorTests()
    {
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => $"hashed:{p}");
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string plain, string hash) => hash == $"hashed:{plain}");

        _tokenVerifierMock.Setup(t => t.GenerateSecret()).Returns("TESTSECRETBASE32");
        _tokenVerifierMock.Setup(t => t.GenerateQrCodeUri(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string account, string secret, string issuer) => $"otpauth://totp/{issuer}:{account}?secret={secret}");
        _tokenVerifierMock.Setup(t => t.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string secret, string code) => code == "123456");
    }

    #region EnableTwoFactor

    [Fact]
    public void EnableTwoFactor_ValidUser_ShouldGenerateSecretAndReturnQrCodeUri()
    {
        var user = CreateUser();

        var qrCodeUri = user.EnableTwoFactor(_tokenVerifierMock.Object);

        user.TwoFactorSecret.Should().Be("TESTSECRETBASE32");
        user.TwoFactorEnabled.Should().BeFalse();
        qrCodeUri.Should().NotBeNullOrEmpty();
        qrCodeUri.Should().Contain("otpauth://totp/");
    }

    [Fact]
    public void EnableTwoFactor_AlreadyEnabled_ShouldThrowException()
    {
        var user = CreateUser();
        user.EnableTwoFactor(_tokenVerifierMock.Object);
        user.ConfirmTwoFactor("123456", _tokenVerifierMock.Object);

        var act = () => user.EnableTwoFactor(_tokenVerifierMock.Object);

        act.Should().Throw<UserAuthDomainException>()
            .Where(ex => ex.Message.Contains("已启用"));
    }

    [Fact]
    public void EnableTwoFactor_NullTokenVerifier_ShouldThrowException()
    {
        var user = CreateUser();

        var act = () => user.EnableTwoFactor(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ConfirmTwoFactor

    [Fact]
    public void ConfirmTwoFactor_ValidCode_ShouldEnableTwoFactor()
    {
        var user = CreateUser();
        user.EnableTwoFactor(_tokenVerifierMock.Object);

        user.ConfirmTwoFactor("123456", _tokenVerifierMock.Object);

        user.TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public void ConfirmTwoFactor_InvalidCode_ShouldThrowException()
    {
        var user = CreateUser();
        user.EnableTwoFactor(_tokenVerifierMock.Object);

        var act = () => user.ConfirmTwoFactor("000000", _tokenVerifierMock.Object);

        act.Should().Throw<UserAuthDomainException>()
            .Where(ex => ex.Message.Contains("验证码无效"));
    }

    [Fact]
    public void ConfirmTwoFactor_EmptyCode_ShouldThrowException()
    {
        var user = CreateUser();
        user.EnableTwoFactor(_tokenVerifierMock.Object);

        var act = () => user.ConfirmTwoFactor("", _tokenVerifierMock.Object);

        act.Should().Throw<UserAuthDomainException>()
            .Where(ex => ex.Message.Contains("验证码不可为空"));
    }

    [Fact]
    public void ConfirmTwoFactor_AlreadyConfirmed_ShouldThrowException()
    {
        var user = CreateUser();
        user.EnableTwoFactor(_tokenVerifierMock.Object);
        user.ConfirmTwoFactor("123456", _tokenVerifierMock.Object);

        var act = () => user.ConfirmTwoFactor("123456", _tokenVerifierMock.Object);

        act.Should().Throw<UserAuthDomainException>()
            .Where(ex => ex.Message.Contains("已确认"));
    }

    [Fact]
    public void ConfirmTwoFactor_NotInitiated_ShouldThrowException()
    {
        var user = CreateUser();

        var act = () => user.ConfirmTwoFactor("123456", _tokenVerifierMock.Object);

        act.Should().Throw<UserAuthDomainException>()
            .Where(ex => ex.Message.Contains("先启用"));
    }

    #endregion

    #region DisableTwoFactor

    [Fact]
    public void DisableTwoFactor_Enabled_ShouldClearSecretAndDisable()
    {
        var user = CreateUser();
        user.EnableTwoFactor(_tokenVerifierMock.Object);
        user.ConfirmTwoFactor("123456", _tokenVerifierMock.Object);

        user.DisableTwoFactor();

        user.TwoFactorEnabled.Should().BeFalse();
        user.TwoFactorSecret.Should().BeNull();
    }

    [Fact]
    public void DisableTwoFactor_NotEnabled_ShouldThrowException()
    {
        var user = CreateUser();

        var act = () => user.DisableTwoFactor();

        act.Should().Throw<UserAuthDomainException>()
            .Where(ex => ex.Message.Contains("未启用"));
    }

    #endregion

    #region VerifyTwoFactorCode

    [Fact]
    public void VerifyTwoFactorCode_ValidCode_ShouldReturnTrue()
    {
        var user = CreateUser();
        user.EnableTwoFactor(_tokenVerifierMock.Object);
        user.ConfirmTwoFactor("123456", _tokenVerifierMock.Object);

        var result = user.VerifyTwoFactorCode("123456", _tokenVerifierMock.Object);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyTwoFactorCode_InvalidCode_ShouldReturnFalse()
    {
        var user = CreateUser();
        user.EnableTwoFactor(_tokenVerifierMock.Object);
        user.ConfirmTwoFactor("123456", _tokenVerifierMock.Object);

        var result = user.VerifyTwoFactorCode("000000", _tokenVerifierMock.Object);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyTwoFactorCode_NotEnabled_ShouldThrowException()
    {
        var user = CreateUser();

        var act = () => user.VerifyTwoFactorCode("123456", _tokenVerifierMock.Object);

        act.Should().Throw<UserAuthDomainException>()
            .Where(ex => ex.Message.Contains("未启用"));
    }

    [Fact]
    public void VerifyTwoFactorCode_EmptyCode_ShouldReturnFalse()
    {
        var user = CreateUser();
        user.EnableTwoFactor(_tokenVerifierMock.Object);
        user.ConfirmTwoFactor("123456", _tokenVerifierMock.Object);

        var result = user.VerifyTwoFactorCode("", _tokenVerifierMock.Object);

        result.Should().BeFalse();
    }

    #endregion

    #region ResetPassword

    [Fact]
    public void ResetPassword_ValidHash_ShouldUpdatePasswordAndPublishEvent()
    {
        var user = CreateUser();
        var newHash = _hasherMock.Object.Hash("NewPass456!");

        user.ResetPassword(newHash, _hasherMock.Object);

        user.PasswordHash.Should().Be(newHash);
        user.DomainEvents.Should().Contain(e => e is UserPasswordChangedEvent);
    }

    [Fact]
    public void ResetPassword_EmptyHash_ShouldThrowException()
    {
        var user = CreateUser();

        var act = () => user.ResetPassword("", _hasherMock.Object);

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region PublishForgotPasswordRequested

    [Fact]
    public void PublishForgotPasswordRequested_ValidToken_ShouldPublishEvent()
    {
        var user = CreateUser();

        user.PublishForgotPasswordRequested("test-reset-token");

        user.DomainEvents.Should().Contain(e => e is ForgotPasswordRequestedEvent);
    }

    [Fact]
    public void PublishForgotPasswordRequested_EmptyToken_ShouldThrowException()
    {
        var user = CreateUser();

        var act = () => user.PublishForgotPasswordRequested("");

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    private User CreateUser(string? passwordHash = null)
    {
        var user = User.Create(
            Guid.NewGuid(), "testuser", "test@test.com", null,
            passwordHash ?? _hasherMock.Object.Hash("Pass123!"),
            "Test Nick", null);
        user.ClearDomainEvents();
        return user;
    }
}