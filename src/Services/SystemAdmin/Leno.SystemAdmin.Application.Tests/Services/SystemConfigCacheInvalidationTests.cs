using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Application.Abstractions;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Application.Tests.Services;

/// <summary>
/// H-03 验证 SystemConfigAppService 在 Create/Update/Enable/Disable 后主动失效 Redis 缓存。
/// </summary>
public sealed class SystemConfigCacheInvalidationTests
{
    private readonly Mock<ISystemConfigRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISystemConfigCache> _cacheMock;
    private readonly SystemConfigAppService _service;

    public SystemConfigCacheInvalidationTests()
    {
        _cacheMock = new Mock<ISystemConfigCache>();
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new SystemConfigAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            NullLogger<SystemConfigAppService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_Should_Invalidate_Cache_By_Key()
    {
        var dto = new SaveSystemConfigDto
        {
            Key = "test.key", Value = "v", Group = "g", Description = null, IsEncrypted = false
        };

        await _service.CreateAsync(dto, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Invalidate_Cache_By_Key()
    {
        var configId = Guid.NewGuid();
        var existing = SystemConfig.Create(configId, "test.key", "old", "g", null, false);
        _repoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dto = new UpdateSystemConfigDto { Value = "new", Description = null, IsEncrypted = false };
        await _service.UpdateAsync(configId, dto, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_Should_Invalidate_Cache_By_Key()
    {
        var configId = Guid.NewGuid();
        var existing = SystemConfig.Create(configId, "test.key", "v", "g", null, false);
        _repoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _service.DisableAsync(configId, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.key", It.IsAny<CancellationToken>()), Times.Once);
    }
}
