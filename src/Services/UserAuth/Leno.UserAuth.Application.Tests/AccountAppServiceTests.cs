using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Moq;

namespace Leno.UserAuth.Application.Tests;

/// <summary>
/// AccountAppService 单元测试，聚焦 BindExternalLogin/UnbindExternalLogin 的
/// SaveEntitiesAsync 调用契约与审计日志写入验证。
/// </summary>
public class AccountAppServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IOAuth2ProviderResolver> _providerResolverMock = new();
    private readonly Mock<IExternalAuthService> _authServiceMock = new();

    private readonly AccountAppService _sut;

    public AccountAppServiceTests()
    {
        _sut = new AccountAppService(
            _userRepositoryMock.Object,
            _auditLogRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _providerResolverMock.Object);
    }

    private static User CreateUser(Guid userId)
    {
        return User.Create(
            userId,
            "u1",
            "u1@example.com",
            "+8613800138000",
            "hashed:Password123",
            "U1");
    }

    private void SetupBindExternalLogin(Guid userId, User user)
    {
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _providerResolverMock.Setup(r => r.Resolve("google")).Returns(_authServiceMock.Object);
        _authServiceMock.Setup(s => s.ExchangeCodeAsync("code", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLoginInfo("google", "g-1", "u1@example.com", "U1", null));
        _userRepositoryMock.Setup(r => r.FindByExternalLoginAsync("google", "g-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
    }

    [Fact]
    public async Task BindExternalLoginAsync_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        SetupBindExternalLogin(userId, user);

        var dto = new BindExternalLoginDto
        {
            Provider = "google",
            Code = "code",
            RedirectUri = "https://app.leno.com/cb"
        };

        // Act
        await _sut.BindExternalLoginAsync(userId, dto, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnbindExternalLoginAsync_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        // 先绑定外部登录，再解绑
        user.LinkExternalLogin("google", "g-1", "u1@example.com", "U1", null);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _sut.UnbindExternalLoginAsync(userId, "google", CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BindExternalLoginAsync_Should_Write_AuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        SetupBindExternalLogin(userId, user);

        var dto = new BindExternalLoginDto
        {
            Provider = "google",
            Code = "code",
            RedirectUri = "https://app.leno.com/cb"
        };

        // Act
        await _sut.BindExternalLoginAsync(userId, dto, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
            log.Action == "ExternalLoginBind" &&
            log.ResourceType == "User" &&
            log.OperatorId == userId &&
            log.ResourceId == userId.ToString()), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnbindExternalLoginAsync_Should_Write_AuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        user.LinkExternalLogin("google", "g-1", "u1@example.com", "U1", null);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _sut.UnbindExternalLoginAsync(userId, "google", CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
            log.Action == "ExternalLoginUnbind" &&
            log.ResourceType == "User" &&
            log.OperatorId == userId &&
            log.ResourceId == userId.ToString()), It.IsAny<CancellationToken>()), Times.Once);
    }
}
