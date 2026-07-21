using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Application.Tests.Services;

/// <summary>
/// 验证 <see cref="RateLimitRule"/> 聚合根携带 <c>Version</c> RowVersion 字段，
/// <see cref="RateLimitRuleAppService.ToDto"/> 正确投影该字段以支持控制器层 <c>DbUpdateConcurrencyException</c> 捕获。
/// </summary>
public sealed class RateLimitRuleConcurrencyTests
{
    private readonly Mock<IRateLimitRuleRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RateLimitRuleAppService _service;

    public RateLimitRuleConcurrencyTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new RateLimitRuleAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<RateLimitRuleAppService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_Should_Return_Dto_With_Version_Field_Present()
    {
        var dto = new SaveRateLimitRuleDto
        {
            TargetApi = "/api/orders",
            TargetContext = "userId",
            Limit = 100,
            WindowSeconds = 60,
            Algorithm = LimitAlgorithm.TokenBucket,
            Scope = LimitScope.User
        };

        var result = await _service.CreateAsync(dto, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Version);
        Assert.Equal("/api/orders", result.TargetApi);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Project_Version_From_Aggregate()
    {
        var ruleId = Guid.NewGuid();
        var rule = RateLimitRule.Create(
            ruleId,
            "/api/orders",
            "userId",
            100,
            60,
            LimitAlgorithm.TokenBucket,
            LimitScope.User);
        // 模拟 EF Core 加载后的 RowVersion（数据库生成的非空字节数组）
        SetVersion(rule, new byte[] { 1, 2, 3, 4 });
        _repoMock.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var result = await _service.GetByIdAsync(ruleId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result!.Version);
    }

    [Fact]
    public void RateLimitRule_Aggregate_Should_Have_Version_Property()
    {
        var rule = RateLimitRule.Create(
            Guid.NewGuid(),
            "/api/test",
            null,
            10,
            60,
            LimitAlgorithm.FixedWindow,
            LimitScope.Global);

        Assert.NotNull(rule.Version);
        // 新建实例 Version 为默认空数组（数据库持久化时由 IsRowVersion() 自动生成）
        Assert.Empty(rule.Version);
    }

    private static void SetVersion(RateLimitRule rule, byte[] version)
    {
        // 通过反射设置 Version 字段模拟数据库加载后的状态
        var prop = typeof(RateLimitRule).GetProperty(nameof(RateLimitRule.Version));
        Assert.NotNull(prop);
        prop!.SetValue(rule, version);
    }
}
