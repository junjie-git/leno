using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.Services;
using Moq;

namespace Leno.UserAuth.Application.Tests;

/// <summary>
/// UserAdminAppService 单元测试，聚焦 SuspendAsync 的 RefreshToken 撤销
/// 与 ResumeAsync 不撤销令牌的语义验证。
/// </summary>
public class UserAdminAppServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokenStoreMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<IJwtRevocationService> _jwtRevocationMock = new();

    public UserAdminAppServiceTests()
    {
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => $"hashed:{p}");
    }

    private UserAdminAppService CreateSut()
    {
        return new UserAdminAppService(
            _userRepositoryMock.Object,
            _auditLogRepositoryMock.Object,
            _refreshTokenStoreMock.Object,
            _jwtRevocationMock.Object,
            _unitOfWorkMock.Object);
    }

    private User CreateUser(Guid userId)
    {
        return User.Create(
            userId,
            "badguy",
            "bad@example.com",
            "+8613800138000",
            _hasherMock.Object.Hash("Password1"),
            "Bad");
    }

    [Fact]
    public async Task SuspendAsync_Should_Revoke_All_Refresh_Tokens_For_Target_User()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var user = CreateUser(targetId);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        await sut.SuspendAsync(
            targetId,
            new SuspendUserDto { Reason = "abuse", DurationMinutes = 30 },
            operatorId,
            CancellationToken.None);

        // Assert
        _refreshTokenStoreMock.Verify(s => s.RevokeAllAsync(targetId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeAsync_On_Disabled_User_Should_Not_Revoke_Tokens()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var user = CreateUser(targetId);
        user.Disable("test", operatorId);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        await sut.ResumeAsync(targetId, operatorId, CancellationToken.None);

        // Assert：恢复操作不应撤销令牌（用户已通过审核恢复）
        _refreshTokenStoreMock.Verify(s => s.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResumeAsync_On_Locked_User_Should_Not_Revoke_Tokens()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var user = CreateUser(targetId);
        user.Lock("test", TimeSpan.FromMinutes(30));
        _userRepositoryMock.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        await sut.ResumeAsync(targetId, operatorId, CancellationToken.None);

        // Assert：恢复操作不应撤销令牌
        _refreshTokenStoreMock.Verify(s => s.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
