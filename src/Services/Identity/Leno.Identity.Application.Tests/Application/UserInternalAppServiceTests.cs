using Leno.Identity.Application.DTOs;
using Leno.Identity.Application.Services;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Tests.Application;

/// <summary>
/// UserInternalAppService 单元测试（Task A2 补齐）。
/// 覆盖脱敏联系方式查询、完整 PII 查询、用户不存在异常、空值处理等场景。
/// </summary>
public class UserInternalAppServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ILogger<UserInternalAppService>> _loggerMock = new();
    private readonly UserInternalAppService _sut;

    public UserInternalAppServiceTests()
    {
        _sut = new UserInternalAppService(_userRepoMock.Object, _loggerMock.Object);
    }

    #region GetContactsAsync (masked)

    [Fact]
    public async Task GetContactsAsync_With_Phone_And_Email_Should_Return_Masked_Contacts()
    {
        // Arrange：手机号长度 > 7 应保留前 3 后 4
        var user = CreateUser(
            email: "alice@example.com",
            phone: "+8613800138000");
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetContactsAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(user.Id);
        result.PhoneNumber.Should().Be("+86****8000", "应保留前 3 后 4 位");
        result.Email.Should().Be("a***@example.com", "应保留首字符与域名");
    }

    [Fact]
    public async Task GetContactsAsync_With_Short_Phone_Should_Return_Full_Mask()
    {
        // Arrange：手机号长度 ≤ 7 应全掩码为 ****
        var user = CreateUser(
            email: "bob@example.com",
            phone: "+12345");
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetContactsAsync(user.Id);

        // Assert
        result.PhoneNumber.Should().Be("****");
    }

    [Fact]
    public async Task GetContactsAsync_With_Null_Phone_Should_Return_Null_Phone_Field()
    {
        // Arrange：OAuth 注册用户可能无手机号
        var user = CreateUser(email: "oauth@example.com", phone: null);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetContactsAsync(user.Id);

        // Assert
        result.PhoneNumber.Should().BeNull();
        result.Email.Should().NotBeNull();
    }

    [Fact]
    public async Task GetContactsAsync_With_Missing_User_Should_Throw_DomainException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = async () => await _sut.GetContactsAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*用户不存在*");
    }

    [Fact]
    public async Task GetContactsAsync_With_Empty_UserId_Should_Throw_ArgumentException()
    {
        var act = async () => await _sut.GetContactsAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetFullContactsAsync (PII)

    [Fact]
    public async Task GetFullContactsAsync_With_Phone_And_Email_Should_Return_Full_Contacts()
    {
        // Arrange
        var user = CreateUser(
            email: "alice@example.com",
            phone: "+8613800138000");
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetFullContactsAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(user.Id);
        result.PhoneNumber.Should().Be("+8613800138000", "完整 PII 查询应返回原始值");
        result.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task GetFullContactsAsync_With_Null_Phone_Should_Return_Null_Phone()
    {
        // Arrange
        var user = CreateUser(email: "bob@example.com", phone: null);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetFullContactsAsync(user.Id);

        // Assert
        result.PhoneNumber.Should().BeNull();
        result.Email.Should().Be("bob@example.com");
    }

    [Fact]
    public async Task GetFullContactsAsync_With_Null_Email_Should_Return_Null_Email()
    {
        // Arrange：构造无邮箱用户（仅手机号注册）
        var id = Guid.NewGuid();
        var user = User.Create(
            id: id,
            username: "phoneonly_" + id.ToString("N")[..8],
            email: null,
            phoneNumber: "+8613800138000",
            passwordHash: "hashed:Pass123!",
            nickname: "PhoneUser");
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetFullContactsAsync(user.Id);

        // Assert
        result.Email.Should().BeNull();
        result.PhoneNumber.Should().Be("+8613800138000");
    }

    [Fact]
    public async Task GetFullContactsAsync_With_Missing_User_Should_Throw_DomainException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = async () => await _sut.GetFullContactsAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*用户不存在*");
    }

    [Fact]
    public async Task GetFullContactsAsync_With_Empty_UserId_Should_Throw_ArgumentException()
    {
        var act = async () => await _sut.GetFullContactsAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Default-Safety Property

    [Fact]
    public async Task GetContactsAsync_Should_Not_Leak_PII_By_Default()
    {
        // 安全属性测试：默认 GetContactsAsync 不应泄露完整 PII
        var user = CreateUser(
            email: "secret@example.com",
            phone: "+8613800138000");
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var masked = await _sut.GetContactsAsync(user.Id);
        var full = await _sut.GetFullContactsAsync(user.Id);

        masked.PhoneNumber.Should().NotBe(full.PhoneNumber, "脱敏 DTO 的手机号不应与完整 DTO 相同");
        masked.Email.Should().NotBe(full.Email, "脱敏 DTO 的邮箱不应与完整 DTO 相同");
        masked.PhoneNumber.Should().Contain("*");
        masked.Email.Should().Contain("*");
    }

    #endregion

    /// <summary>创建测试用户（默认含邮箱与手机号）。</summary>
    private static User CreateUser(string? email, string? phone)
    {
        var id = Guid.NewGuid();
        // 必须提供邮箱、手机号或密码之一，否则 User.Create 会抛异常
        var passwordHash = "hashed:Pass123!";
        return User.Create(
            id: id,
            username: "testuser_" + id.ToString("N")[..8],
            email: email,
            phoneNumber: phone,
            passwordHash: passwordHash,
            nickname: "TestUser");
    }
}
