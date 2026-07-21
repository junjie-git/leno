using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Cache;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Application.Tests.Services;

/// <summary>
/// H-03 验证 FeatureFlagAppService 在 Update/Enable/Disable 后主动失效 Redis 缓存。
/// </summary>
public sealed class FeatureFlagCacheInvalidationTests
{
    private readonly Mock<IFeatureFlagRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IFeatureFlagEvaluator> _evaluatorMock = new();
    private readonly Mock<FeatureFlagCache> _cacheMock;
    private readonly FeatureFlagAppService _service;

    public FeatureFlagCacheInvalidationTests()
    {
        _cacheMock = new Mock<FeatureFlagCache>(
            Mock.Of<IConnectionMultiplexer>(),
            NullLogger<FeatureFlagCache>.Instance);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new FeatureFlagAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            _evaluatorMock.Object,
            _cacheMock.Object,
            NullLogger<FeatureFlagAppService>.Instance);
    }

    [Fact]
    public async Task UpdateAsync_Should_Invalidate_Cache_By_FlagKey()
    {
        var flagId = Guid.NewGuid();
        var existing = FeatureFlag.Create(flagId, "test.flag", "测试开关", null,
            FeatureFlagStrategy.Global, null);
        _repoMock.Setup(r => r.GetByIdAsync(flagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dto = new UpdateFeatureFlagDto
        {
            Name = "更新开关",
            Description = null,
            Strategy = FeatureFlagStrategy.Global,
            Rules = null
        };

        await _service.UpdateAsync(flagId, dto, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.flag", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_Should_Invalidate_Cache_By_FlagKey()
    {
        var flagId = Guid.NewGuid();
        var existing = FeatureFlag.Create(flagId, "test.flag", "测试开关", null,
            FeatureFlagStrategy.Global, null);
        _repoMock.Setup(r => r.GetByIdAsync(flagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _service.EnableAsync(flagId, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.flag", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_Should_Invalidate_Cache_By_FlagKey()
    {
        var flagId = Guid.NewGuid();
        var existing = FeatureFlag.Create(flagId, "test.flag", "测试开关", null,
            FeatureFlagStrategy.Global, null);
        _repoMock.Setup(r => r.GetByIdAsync(flagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _service.DisableAsync(flagId, CancellationToken.None);

        _cacheMock.Verify(c => c.RemoveAsync("test.flag", It.IsAny<CancellationToken>()), Times.Once);
    }
}
