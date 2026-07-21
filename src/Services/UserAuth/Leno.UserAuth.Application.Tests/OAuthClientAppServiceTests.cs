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
/// SaveEntitiesAsync 调用契约与审计日志写入验证。
/// </summary>
public class OAuthClientAppServiceTests
{
    private readonly Mock<IOAuthClientRepository> _repositoryMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IClientSecretEncryptionService> _encryptionServiceMock = new();

    private readonly OAuthClientAppService _sut;
    private readonly Guid _operatorId = Guid.NewGuid();

    public OAuthClientAppServiceTests()
    {
        _encryptionServiceMock.Setup(e => e.Encrypt(It.IsAny<string>()))
            .Returns((string plain) => $"encrypted:{plain}");

        _sut = new OAuthClientAppService(
            _repositoryMock.Object,
            _auditLogRepositoryMock.Object,
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
        await _sut.UpdateAsync("google", CreateUpdateDto(), _operatorId, CancellationToken.None);

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
        await _sut.UpdateAsync("google", CreateUpdateDto(), _operatorId, CancellationToken.None);

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
        await _sut.EnableAsync("google", _operatorId, CancellationToken.None);

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
        await _sut.DisableAsync("google", _operatorId, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Should_Write_AuditLog()
    {
        // Arrange
        var client = CreateExistingClient();
        _repositoryMock.Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        // Act
        await _sut.UpdateAsync("google", CreateUpdateDto(), _operatorId, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
            log.Action == "OAuthClientUpdate" &&
            log.ResourceType == "OAuthClient" &&
            log.OperatorId == _operatorId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_Should_Write_AuditLog()
    {
        // Arrange
        var client = CreateExistingClient();
        client.Disable();
        _repositoryMock.Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        // Act
        await _sut.EnableAsync("google", _operatorId, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
            log.Action == "OAuthClientEnable" &&
            log.ResourceType == "OAuthClient" &&
            log.OperatorId == _operatorId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_Should_Write_AuditLog()
    {
        // Arrange
        var client = CreateExistingClient();
        _repositoryMock.Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        // Act
        await _sut.DisableAsync("google", _operatorId, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
            log.Action == "OAuthClientDisable" &&
            log.ResourceType == "OAuthClient" &&
            log.OperatorId == _operatorId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
