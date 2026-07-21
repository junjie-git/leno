using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Infrastructure.Cache;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Application.Tests.Services;

/// <summary>
/// H-02 验证 SystemConfigAppService 不再依赖 IEventBus，
/// 仅通过 SaveEntitiesAsync 走发件箱投递集成事件。
/// H-03 在此基础上追加 SystemConfigCache 注入以失效缓存。
/// </summary>
public sealed class SystemConfigAppServiceOutboxTests
{
    private readonly Mock<ISystemConfigRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<SystemConfigCache> _cacheMock;
    private readonly SystemConfigAppService _service;

    public SystemConfigAppServiceOutboxTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _cacheMock = new Mock<SystemConfigCache>(
            Mock.Of<IConnectionMultiplexer>(),
            NullLogger<SystemConfigCache>.Instance);
        _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _service = new SystemConfigAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            NullLogger<SystemConfigAppService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_Should_Not_Inject_IEventBus_And_Call_Only_SaveEntitiesAsync()
    {
        var dto = new SaveSystemConfigDto
        {
            Key = "test.key",
            Value = "test-value",
            Group = "test-group",
            Description = null,
            IsEncrypted = false
        };

        var result = await _service.CreateAsync(dto, CancellationToken.None);

        result.Should().NotBeNull();
        result.Key.Should().Be("test.key");
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Call_Only_SaveEntitiesAsync_Without_Manual_Publish()
    {
        var configId = Guid.NewGuid();
        var existing = SystemConfig.Create(configId, "test.key", "old-value", "group", null, false);
        _repoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dto = new UpdateSystemConfigDto { Value = "new-value", Description = null, IsEncrypted = false };

        var result = await _service.UpdateAsync(configId, dto, CancellationToken.None);

        result.Value.Should().Be("new-value");
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
