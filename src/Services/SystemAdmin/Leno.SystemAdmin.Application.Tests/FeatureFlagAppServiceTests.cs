using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Cache;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Application.Tests;

/// <summary>
/// 特性开关管理应用服务单元测试，覆盖创建、更新、启停与评估用例。
/// </summary>
public class FeatureFlagAppServiceTests
{
    private readonly Mock<IFeatureFlagRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IFeatureFlagEvaluator> _evaluatorMock = new();
    private readonly Mock<FeatureFlagCache> _cacheMock;
    private readonly Mock<ILogger<FeatureFlagAppService>> _loggerMock = new();
    private readonly FeatureFlagAppService _sut;

    private static readonly Guid FlagId = Guid.NewGuid();
    private const string FlagKey = "new_checkout_ui";

    public FeatureFlagAppServiceTests()
    {
        _cacheMock = new Mock<FeatureFlagCache>(
            Mock.Of<IConnectionMultiplexer>(),
            NullLogger<FeatureFlagCache>.Instance);
        _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new FeatureFlagAppService(
            _repoMock.Object,
            _uowMock.Object,
            _evaluatorMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_Valid_ShouldCreateEnabledFlag()
    {
        var dto = new SaveFeatureFlagDto
        {
            Key = FlagKey,
            Name = "新版结算页",
            Description = "灰度新版结算 UI",
            Strategy = FeatureFlagStrategy.Percentage,
            Rules = "{\"percent\":30}"
        };

        var result = await _sut.CreateAsync(dto);

        result.FlagId.Should().NotBe(Guid.Empty);
        result.Key.Should().Be(FlagKey);
        result.Name.Should().Be("新版结算页");
        result.IsEnabled.Should().BeTrue();
        result.Strategy.Should().Be(FeatureFlagStrategy.Percentage);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<FeatureFlag>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NullDto_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.CreateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<FeatureFlag>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Existing_ShouldUpdateFields()
    {
        var flag = FeatureFlag.Create(FlagId, FlagKey, "新版结算页", null, FeatureFlagStrategy.Global, null);
        _repoMock
            .Setup(r => r.GetByIdAsync(FlagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flag);

        var result = await _sut.UpdateAsync(FlagId, new UpdateFeatureFlagDto
        {
            Name = "新版结算页 V2",
            Description = "灰度范围扩大",
            Strategy = FeatureFlagStrategy.UserWhitelist,
            Rules = "{\"users\":[\"u1\"]}"
        });

        result.Name.Should().Be("新版结算页 V2");
        result.Strategy.Should().Be(FeatureFlagStrategy.UserWhitelist);
        flag.DomainEvents.Should().NotBeEmpty();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_Existing_ShouldEnableAndSave()
    {
        var flag = FeatureFlag.Create(FlagId, FlagKey, "开关", null, FeatureFlagStrategy.Global, null);
        flag.Disable();
        _repoMock
            .Setup(r => r.GetByIdAsync(FlagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flag);

        await _sut.EnableAsync(FlagId);

        flag.IsEnabled.Should().BeTrue();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_Existing_ShouldDisableAndSave()
    {
        var flag = FeatureFlag.Create(FlagId, FlagKey, "开关", null, FeatureFlagStrategy.Global, null);
        _repoMock
            .Setup(r => r.GetByIdAsync(FlagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flag);

        await _sut.DisableAsync(FlagId);

        flag.IsEnabled.Should().BeFalse();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_NotFound_ShouldThrowInvalidOperationException()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(FlagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureFlag?)null);

        var act = () => _sut.EnableAsync(FlagId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*特性开关*不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByKeyAsync_Existing_ShouldReturnDto()
    {
        var flag = FeatureFlag.Create(FlagId, FlagKey, "开关", null, FeatureFlagStrategy.Global, null);
        _repoMock
            .Setup(r => r.GetByKeyAsync(FlagKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flag);

        var result = await _sut.GetByKeyAsync(FlagKey);

        result.Should().NotBeNull();
        result!.Key.Should().Be(FlagKey);
        result.FlagId.Should().Be(FlagId);
    }

    [Fact]
    public async Task GetByKeyAsync_NotExisting_ShouldReturnNull()
    {
        _repoMock
            .Setup(r => r.GetByKeyAsync(FlagKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureFlag?)null);

        var result = await _sut.GetByKeyAsync(FlagKey);

        result.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnPaginatedResult()
    {
        var flag = FeatureFlag.Create(FlagId, FlagKey, "开关", null, FeatureFlagStrategy.Global, null);
        _repoMock
            .Setup(r => r.QueryAsync(null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlag> { flag });
        _repoMock
            .Setup(r => r.CountAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.QueryAsync(null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldDelegateToEvaluator()
    {
        _evaluatorMock
            .Setup(e => e.EvaluateAsync(FlagKey, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.EvaluateAsync(new EvaluateFlagDto
        {
            FlagKey = FlagKey,
            Context = new Dictionary<string, string> { ["userId"] = "u1" }
        });

        result.Should().BeTrue();
        _evaluatorMock.Verify(e => e.EvaluateAsync(FlagKey, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_NullDto_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.EvaluateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
