using FluentValidation;
using FluentValidation.Results;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Exceptions;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.UserAuth.Application.Tests;

public class UserAppServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<IUserUniquenessChecker> _uniquenessMock = new();
    private readonly Mock<ITokenService> _tokenMock = new();
    private readonly Mock<ITokenVerifier> _tokenVerifierMock = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokenMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IValidator<RegisterDto>> _registerValidatorMock = new();
    private readonly Mock<IValidator<LoginDto>> _loginValidatorMock = new();
    private readonly Mock<IValidator<UpdateProfileDto>> _updateProfileValidatorMock = new();
    private readonly Mock<IValidator<ChangePasswordDto>> _changePasswordValidatorMock = new();
    private readonly Mock<IOAuthStateStore> _oauthStateStoreMock = new();
    private readonly Mock<ITwoFactorTempTokenStore> _twoFactorTempTokenStoreMock = new();
    private readonly Mock<IPasswordResetTokenStore> _passwordResetTokenStoreMock = new();
    private readonly Mock<IOAuth2ProviderResolver> _providerResolverMock = new();
    private readonly UserAppService _sut;

    public UserAppServiceTests()
    {
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => $"hashed:{p}");
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string plain, string hash) => hash == $"hashed:{plain}");

        _tokenMock.Setup(t => t.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .Returns("access-token");
        _tokenMock.Setup(t => t.AccessTokenExpirySeconds).Returns(3600);

        _refreshTokenMock.Setup(r => r.IssueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        _registerValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<RegisterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _loginValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _updateProfileValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<UpdateProfileDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _changePasswordValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        // 2FA 临时令牌存储默认返回一个固定令牌，便于登录 2FA 路径测试
        _twoFactorTempTokenStoreMock
            .Setup(s => s.IssueAsync(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("temp-token");
        // 密码重置令牌存储默认返回一个固定令牌
        _passwordResetTokenStoreMock
            .Setup(s => s.IssueAsync(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("reset-token");

        _sut = new UserAppService(
            _userRepoMock.Object,
            _hasherMock.Object,
            _uniquenessMock.Object,
            _tokenMock.Object,
            _tokenVerifierMock.Object,
            _refreshTokenMock.Object,
            Mock.Of<IJwtRevocationService>(),
            _uowMock.Object,
            _registerValidatorMock.Object,
            _loginValidatorMock.Object,
            _updateProfileValidatorMock.Object,
            _changePasswordValidatorMock.Object,
            _providerResolverMock.Object,
            _oauthStateStoreMock.Object,
            _twoFactorTempTokenStoreMock.Object,
            _passwordResetTokenStoreMock.Object,
            Options.Create(new OAuth2Options()));
    }

    #region RegisterAsync

    [Fact]
    public async Task RegisterAsync_ValidInput_ShouldReturnTokenDto()
    {
        _uniquenessMock.Setup(u => u.IsUsernameUniqueAsync("testuser", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _uniquenessMock.Setup(u => u.IsEmailUniqueAsync("test@test.com", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dto = new RegisterDto
        {
            Username = "testuser",
            Email = "test@test.com",
            Password = "Pass123!",
            Nickname = "Test Nick"
        };

        var result = await _sut.RegisterAsync(dto);

        result.Should().NotBeNull();
        result.Username.Should().Be("testuser");
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.ExpiresIn.Should().Be(3600);
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateUsername_ShouldThrowDomainException()
    {
        _uniquenessMock.Setup(u => u.IsUsernameUniqueAsync("testuser", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var dto = new RegisterDto
        {
            Username = "testuser",
            Email = "test@test.com",
            Password = "Pass123!",
            Nickname = "Test Nick"
        };

        var act = () => _sut.RegisterAsync(dto);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*用户名已被注册*");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ShouldThrowDomainException()
    {
        _uniquenessMock.Setup(u => u.IsUsernameUniqueAsync("testuser", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _uniquenessMock.Setup(u => u.IsEmailUniqueAsync("test@test.com", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var dto = new RegisterDto
        {
            Username = "testuser",
            Email = "test@test.com",
            Password = "Pass123!",
            Nickname = "Test Nick"
        };

        var act = () => _sut.RegisterAsync(dto);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*邮箱已被注册*");
    }

    [Fact]
    public async Task RegisterAsync_ValidationFailure_ShouldThrowValidationException()
    {
        _registerValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<RegisterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Username", "用户名格式错误") }));

        var dto = new RegisterDto { Username = "", Password = "", Nickname = "" };

        var act = () => _sut.RegisterAsync(dto);

        await act.Should().ThrowAsync<UserAuthValidationException>();
    }

    #endregion

    #region LoginAsync

    [Fact]
    public async Task LoginAsync_ValidCredentials_ShouldReturnTokenDto()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var dto = new LoginDto { Account = "testuser", Password = "Pass123!" };

        var result = await _sut.LoginAsync(dto);

        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_DisabledUser_ShouldThrowDomainException()
    {
        var user = CreateUser();
        user.Disable("test", Guid.NewGuid());
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var dto = new LoginDto { Account = "testuser", Password = "Pass123!" };

        var act = () => _sut.LoginAsync(dto);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*已被禁用*");
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ShouldThrowUnauthorized()
    {
        _userRepoMock.Setup(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var dto = new LoginDto { Account = "nonexistent", Password = "Pass123!" };

        var act = () => _sut.LoginAsync(dto);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*账号或密码错误*");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ShouldThrowUnauthorized()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var dto = new LoginDto { Account = "testuser", Password = "wrong" };

        var act = () => _sut.LoginAsync(dto);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*账号或密码错误*");
    }

    [Fact]
    public async Task LoginAsync_LockedExpired_ShouldUnlockAndLogin()
    {
        var user = CreateUser();
        user.Lock("test", TimeSpan.FromMinutes(-1)); // expired
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var dto = new LoginDto { Account = "testuser", Password = "Pass123!" };

        var result = await _sut.LoginAsync(dto);

        result.Should().NotBeNull();
        user.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public async Task LoginAsync_ByEmail_ShouldFindUser()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var dto = new LoginDto { Account = "test@test.com", Password = "Pass123!" };

        var result = await _sut.LoginAsync(dto);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task LoginAsync_Should_Retry_When_DbUpdateConcurrencyException_Thrown()
    {
        // Arrange：构造一个账户并让 SaveEntitiesAsync 第一次抛并发异常，第二次成功
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var saveCallCount = 0;
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                saveCallCount++;
                if (saveCallCount == 1)
                {
                    throw new DbUpdateConcurrencyException("RowVersion mismatch");
                }
                return Task.CompletedTask;
            });

        // Act：登录失败一次（密码错误），第一次 Save 抛并发异常，应自动重试
        var dto = new LoginDto { Account = "testuser", Password = "wrong" };

        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
            _sut.LoginAsync(dto, CancellationToken.None));

        // Assert：至少重试一次，证明并发重试逻辑生效
        Assert.True(saveCallCount >= 2);
    }

    #endregion

    #region ChangePasswordAsync

    [Fact]
    public async Task ChangePasswordAsync_ValidInput_ShouldSucceed()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var dto = new ChangePasswordDto { OldPassword = "Pass123!", NewPassword = "NewPass456!" };

        await _sut.Invoking(s => s.ChangePasswordAsync(user.Id, dto))
            .Should().NotThrowAsync();

        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ShouldThrowDomainException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var dto = new ChangePasswordDto { OldPassword = "Pass123!", NewPassword = "NewPass456!" };

        var act = () => _sut.ChangePasswordAsync(Guid.NewGuid(), dto);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*用户不存在*");
    }

    #endregion

    #region GetProfileAsync

    [Fact]
    public async Task GetProfileAsync_ExistingUser_ShouldReturnUserDto()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.GetProfileAsync(user.Id);

        result.Should().NotBeNull();
        result.Username.Should().Be("testuser");
        result.Nickname.Should().Be("Test Nick");
    }

    #endregion

    #region UpdateProfileAsync

    [Fact]
    public async Task UpdateProfileAsync_ValidInput_ShouldUpdateAndReturnDto()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var dto = new UpdateProfileDto { Nickname = "New Nick", AvatarUrl = null };

        var result = await _sut.UpdateProfileAsync(user.Id, dto);

        result.Nickname.Should().Be("New Nick");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region RefreshTokenAsync

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ShouldReturnNewTokens()
    {
        var user = CreateUser();
        _refreshTokenMock.Setup(r => r.ValidateAndRotateAsync("valid-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.Id);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.RefreshTokenAsync("valid-refresh");

        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_ShouldThrowUnauthorized()
    {
        _refreshTokenMock.Setup(r => r.ValidateAndRotateAsync("invalid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var act = () => _sut.RefreshTokenAsync("invalid");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RefreshTokenAsync_EmptyToken_ShouldThrowValidationException()
    {
        var act = () => _sut.RefreshTokenAsync("");

        await act.Should().ThrowAsync<UserAuthValidationException>();
    }

    [Fact]
    public async Task RefreshTokenAsync_Should_Reject_Locked_User_Within_Lock_Window()
    {
        // Arrange
        var user = CreateUser();
        user.Lock("audit test", TimeSpan.FromMinutes(30));

        _refreshTokenMock.Setup(s => s.ValidateAndRotateAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.Id);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UserAuthDomainException>(() =>
            _sut.RefreshTokenAsync("rt", CancellationToken.None));
        Assert.Equal("USER_LOCKED", ex.ErrorCode);
    }

    [Fact]
    public async Task RefreshTokenAsync_Should_Auto_Unlock_When_Lock_Window_Elapsed()
    {
        // Arrange
        var user = CreateUser();
        user.Lock("test", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50); // 等待锁定过期

        _refreshTokenMock.Setup(s => s.ValidateAndRotateAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.Id);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tokenMock.Setup(t => t.GenerateAccessToken(user.Id, It.IsAny<string>(), It.IsAny<Guid?>()))
            .Returns("access");
        _refreshTokenMock.Setup(s => s.IssueAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-rt");

        // Act
        var token = await _sut.RefreshTokenAsync("rt", CancellationToken.None);

        // Assert
        Assert.NotNull(token);
        Assert.Equal(AccountStatus.Active, user.Status);
    }

    [Fact]
    public async Task RefreshTokenAsync_Should_Return_Temp_Token_When_TwoFactor_Enabled()
    {
        // Arrange
        var user = CreateUser();
        var tokenVerifierMock = new Mock<ITokenVerifier>();
        tokenVerifierMock.Setup(v => v.GenerateSecret()).Returns("ABCDEFGHIJKLMNOP");
        tokenVerifierMock.Setup(v => v.GenerateQrCodeUri(It.IsAny<string>(), It.IsAny<string>())).Returns("otpauth://test");
        // 让 TOTP 校验直接通过，使 ConfirmTwoFactor 完成启用流程，无需反射设置私有字段
        tokenVerifierMock.Setup(v => v.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        user.EnableTwoFactor(tokenVerifierMock.Object);
        user.ConfirmTwoFactor("123456", tokenVerifierMock.Object);

        _refreshTokenMock.Setup(s => s.ValidateAndRotateAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.Id);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var token = await _sut.RefreshTokenAsync("rt", CancellationToken.None);

        // Assert
        Assert.NotNull(token);
        Assert.True(token.TwoFactorRequired);
        Assert.False(string.IsNullOrEmpty(token.TempToken));
    }

    #endregion

    #region ForgotPasswordAsync

    [Fact]
    public async Task ForgotPasswordAsync_Should_Call_UpdateAsync_Before_SaveEntitiesAsync()
    {
        // Arrange
        var user = User.Create(
            Guid.NewGuid(),
            "alice",
            "alice@example.com",
            "+8613800138000",
            _hasherMock.Object.Hash("Password123"),
            "Alice");
        _userRepoMock.Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        // 密码重置令牌由存储抽象签发，默认 mock 已返回 "reset-token"

        var callOrder = new List<string>();
        _userRepoMock.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("UpdateAsync"))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("SaveEntitiesAsync"))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ForgotPasswordAsync(new ForgotPasswordDto { Account = "alice@example.com" }, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "UpdateAsync", "SaveEntitiesAsync" }, callOrder);
    }

    #endregion

    #region HandleOAuthCallbackAsync

    [Fact]
    public async Task HandleOAuthCallbackAsync_Should_Not_Silently_Bind_When_Email_Collides_With_Existing_Account()
    {
        // Arrange
        var existingUser = User.Create(
            Guid.NewGuid(),
            "victim",
            "victim@example.com",
            "+8613800138000",
            _hasherMock.Object.Hash("Password123"),
            "Victim");

        var externalInfo = new ExternalLoginInfo(
            "google",
            "attacker-google-id",
            "victim@example.com",
            "Attacker",
            null);

        _userRepoMock
            .Setup(r => r.FindByExternalLoginAsync("google", "attacker-google-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepoMock
            .Setup(r => r.GetByEmailAsync("victim@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        _oauthStateStoreMock
            .Setup(s => s.ConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthStateData("google", "https://app.leno.com/callback"));

        var authServiceMock = new Mock<IExternalAuthService>();
        authServiceMock.SetupGet(s => s.Provider).Returns("google");
        authServiceMock
            .Setup(s => s.ExchangeCodeAsync("code", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalInfo);

        _providerResolverMock
            .Setup(r => r.Resolve("google"))
            .Returns(authServiceMock.Object);

        var service = BuildUserAppService(authServiceMock.Object);

        // Act & Assert：应当抛出异常而非自动绑定
        var ex = await Assert.ThrowsAsync<UserAuthDomainException>(() =>
            service.HandleOAuthCallbackAsync("google", "code", "state", "https://app.leno.com/callback", CancellationToken.None));
        Assert.Equal("OAUTH_EMAIL_ALREADY_USED", ex.ErrorCode);
        // 验证未调用 UpdateAsync（即未绑定到 existingUser）
        _userRepoMock.Verify(r => r.UpdateAsync(existingUser, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_Should_Rename_Instead_Of_Reflection_When_Username_Conflicts()
    {
        // Arrange
        var externalInfo = new ExternalLoginInfo("google", "g-1", "newbie@example.com", "Newbie", null);
        var firstCall = true;
        _uniquenessMock
            .Setup(c => c.IsUsernameUniqueAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string username, Guid? _, CancellationToken _) =>
            {
                if (firstCall)
                {
                    firstCall = false;
                    return false; // 第一次冲突
                }
                return true;
            });

        _oauthStateStoreMock
            .Setup(s => s.ConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthStateData("google", "https://app.leno.com/callback"));

        var authServiceMock = new Mock<IExternalAuthService>();
        authServiceMock.SetupGet(s => s.Provider).Returns("google");
        authServiceMock
            .Setup(s => s.ExchangeCodeAsync("code", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalInfo);

        _providerResolverMock
            .Setup(r => r.Resolve("google"))
            .Returns(authServiceMock.Object);

        _userRepoMock.Setup(r => r.FindByExternalLoginAsync("google", "g-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByEmailAsync("newbie@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = BuildUserAppService(authServiceMock.Object);

        // Act
        var token = await service.HandleOAuthCallbackAsync("google", "code", "state", "https://app.leno.com/callback", CancellationToken.None);

        // Assert
        Assert.NotNull(token);
        _userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u => !string.IsNullOrEmpty(u.Username)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_Should_Reject_When_State_Provider_Mismatch_Callback_Provider()
    {
        // Arrange：state 中存 google，但回调 provider=wechat
        _oauthStateStoreMock
            .Setup(s => s.ConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthStateData("google", "https://app.leno.com/cb"));

        var service = BuildUserAppService();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UserAuthDomainException>(() =>
            service.HandleOAuthCallbackAsync("wechat", "code", "state", "https://app.leno.com/cb", CancellationToken.None));
        Assert.Equal("OAUTH_STATE_PROVIDER_MISMATCH", ex.ErrorCode);
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_Should_Reject_When_State_Parts_Length_Not_Two()
    {
        // Arrange：state 数据格式无效（仅 provider 无分隔符）。
        // 新设计中 state 解析由 RedisOAuthStateStore.ConsumeAsync 内部完成，
        // 解析失败返回 null，UserAppService 看到 null 抛 OAUTH_STATE_EXPIRED。
        _oauthStateStoreMock
            .Setup(s => s.ConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthStateData?)null);

        var service = BuildUserAppService();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UserAuthDomainException>(() =>
            service.HandleOAuthCallbackAsync("google", "code", "state", "https://app.leno.com/cb", CancellationToken.None));
        Assert.Equal("OAUTH_STATE_EXPIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_Should_Reject_When_State_RedirectUri_Mismatch()
    {
        // Arrange：state 中 redirectUri 与回调不一致
        _oauthStateStoreMock
            .Setup(s => s.ConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthStateData("google", "https://app.leno.com/original-cb"));

        var service = BuildUserAppService();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UserAuthDomainException>(() =>
            service.HandleOAuthCallbackAsync("google", "code", "state", "https://evil.example.com/cb", CancellationToken.None));
        Assert.Equal("OAUTH_REDIRECT_URI_MISMATCH", ex.ErrorCode);
    }

    #endregion

    #region ChangePasswordAsync

    [Fact]
    public async Task ChangePasswordAsync_Should_Revoke_All_Refresh_Tokens()
    {
        // Arrange
        var user = User.Create(
            Guid.NewGuid(),
            "alice",
            "alice@example.com",
            "+8613800138000",
            _hasherMock.Object.Hash("OldPassword1"),
            "Alice");
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _sut.ChangePasswordAsync(user.Id, new ChangePasswordDto
        {
            OldPassword = "OldPassword1",
            NewPassword = "NewPassword1"
        }, CancellationToken.None);

        // Assert
        _refreshTokenMock.Verify(s => s.RevokeAllAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ResetPasswordAsync

    [Fact]
    public async Task ResetPasswordAsync_Should_Revoke_All_Refresh_Tokens()
    {
        // Arrange
        var user = User.Create(
            Guid.NewGuid(),
            "alice",
            "alice@example.com",
            "+8613800138000",
            _hasherMock.Object.Hash("OldPassword1"),
            "Alice");
        var token = "reset-token";
        _passwordResetTokenStoreMock
            .Setup(s => s.ValidateAndConsumeAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.Id);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _sut.ResetPasswordAsync(new ResetPasswordDto { Token = token, NewPassword = "NewPassword1" }, CancellationToken.None);

        // Assert
        _refreshTokenMock.Verify(s => s.RevokeAllAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    private UserAppService BuildUserAppService(params IExternalAuthService[] externalAuthServices)
    {
        // 将传入的 IExternalAuthService 通过 _providerResolverMock 暴露给 UserAppService。
        // P1-18 后 UserAppService 不再直接持有 IExternalAuthService 集合，统一通过 IOAuth2ProviderResolver 解析。
        foreach (var svc in externalAuthServices)
        {
            _providerResolverMock
                .Setup(r => r.Resolve(It.Is<string>(p => string.Equals(p, svc.Provider, StringComparison.OrdinalIgnoreCase))))
                .Returns(svc);
        }

        return new UserAppService(
            _userRepoMock.Object,
            _hasherMock.Object,
            _uniquenessMock.Object,
            _tokenMock.Object,
            _tokenVerifierMock.Object,
            _refreshTokenMock.Object,
            Mock.Of<IJwtRevocationService>(),
            _uowMock.Object,
            _registerValidatorMock.Object,
            _loginValidatorMock.Object,
            _updateProfileValidatorMock.Object,
            _changePasswordValidatorMock.Object,
            _providerResolverMock.Object,
            _oauthStateStoreMock.Object,
            _twoFactorTempTokenStoreMock.Object,
            _passwordResetTokenStoreMock.Object,
            Options.Create(new OAuth2Options()));
    }

    private static User CreateUser()
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => $"hashed:{p}");
        return User.Create(
            Guid.NewGuid(), "testuser", "test@test.com", null,
            hasher.Object.Hash("Pass123!"), "Test Nick", null);
    }
}