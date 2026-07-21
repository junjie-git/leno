using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Moq;

namespace Leno.UserAuth.Application.Tests;

/// <summary>
/// OAuthClientAppService 单元测试，聚焦 Update/Enable/Disable 的
/// SaveEntitiesAsync 调用契约，确保 OAuth 客户端配置变更触发的领域事件
/// 与 Outbox 在同事务内写入，避免下游订阅方丢失事件。
/// </summary>
public class OAuthClientAppServiceTests
{
    private readonly Mock<IOAuthClientRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IClientSecretEncryptionService> _encryptionServiceMock = new();

    private readonly OAuthClientAppService _sut;

    public OAuthClientAppServiceTests()
    {
        _encryptionServiceMock.Setup(e => e.Encrypt(It.IsAny<string>()))
            .Returns((string plain) => $"encrypted:{plain}");

        _sut = new OAuthClientAppService(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _encryptionServiceMock.Object);
    }

    private static OAuthClient CreateExistingClient()
    {
        return OAuthClient.Create(
            Guid.NewGuid(),
            "google",
            "client-1",
            "encrypted:old-secret",
            "https://app.leno.com/cb",
            enabled: true);
    }

    private static UpdateOAuthClientDto CreateUpdateDto()
    {
        return new UpdateOAuthClientDto
        {
            ClientId = "client-1-new",
            ClientSecret = "new-secret",
            RedirectUri = "https://app.leno.com/cb-new"
        };
    }

    [Fact]
    public async Task UpdateAsync_Existing_Client_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        // Arrange
        var client = CreateExistingClient();
        _repositoryMock.Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        // Act
        await _sut.UpdateAsync("google", CreateUpdateDto(), CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_New_Client_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthClient?)null);

        // Act
        await _sut.UpdateAsync("google", CreateUpdateDto(), CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnableAsync_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        // Arrange
        var client = CreateExistingClient();
        client.Disable();
        _repositoryMock.Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        // Act
        await _sut.EnableAsync("google", CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisableAsync_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
    {
        // Arrange
        var client = CreateExistingClient();
        _repositoryMock.Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        // Act
        await _sut.DisableAsync("google", CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
