using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.Order.Application.Tests.Services;

/// <summary>
/// 物流公司应用服务 ListAsync（带筛选重载）单元测试。
/// 使用 Moq 模拟 ILogisticsCompanyRepository，验证 keyword 与 status 参数正确透传至仓储层，
/// 并校验聚合根到 DTO 的映射保留 Name/Code/Status 等字段。
/// 仓储层实际的 Contains/状态过滤逻辑由 EfCore 仓储集成测试覆盖，此处只验证服务层契约。
/// </summary>
public class LogisticsCompanyAppServiceListTests
{
    private static readonly string[] ExpectedAllNames = { "顺丰速运", "圆通速递", "中通快递" };
    private static readonly string[] ExpectedAllCodes = { "SF", "YT", "ZT" };
    private static readonly LogisticsCompanyStatus[] ExpectedAllStatuses =
    {
        LogisticsCompanyStatus.Enabled,
        LogisticsCompanyStatus.Enabled,
        LogisticsCompanyStatus.Disabled
    };

    private readonly Mock<ILogisticsCompanyRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly LogisticsCompanyAppService _sut;

    public LogisticsCompanyAppServiceListTests()
    {
        _sut = new LogisticsCompanyAppService(_repoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task ListAsync_WithNullKeywordAndNullStatus_ShouldReturnAll()
    {
        // Arrange：仓储返回全部 3 条（含 Enabled 与 Disabled），服务层应原样映射返回。
        var allCompanies = new List<LogisticsCompany>
        {
            CreateCompany("顺丰速运", "SF", enabled: true),
            CreateCompany("圆通速递", "YT", enabled: true),
            CreateCompany("中通快递", "ZT", enabled: false)
        };

        _repoMock
            .Setup(r => r.ListAsync(1, 20, (string?)null, (LogisticsCompanyStatus?)null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allCompanies);

        // Act
        var result = await _sut.ListAsync(1, 20, null, null);

        // Assert：返回全部 3 条，且字段映射保留 Name/Code/Status。
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Select(c => c.Name).Should().BeEquivalentTo(ExpectedAllNames);
        result.Select(c => c.Code).Should().BeEquivalentTo(ExpectedAllCodes);
        result.Select(c => c.Status).Should().BeEquivalentTo(ExpectedAllStatuses);

        _repoMock.Verify(
            r => r.ListAsync(1, 20, (string?)null, (LogisticsCompanyStatus?)null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListAsync_WithKeyword_ShouldReturnOnlyNameContainingKeyword()
    {
        // Arrange：仓储按 keyword="顺丰" 过滤后仅返回 1 条 Name 含"顺丰"的记录。
        // 仓储层 Contains 模糊匹配的实际行为由 EfCore 仓储集成测试覆盖，此处验证服务层正确透传 keyword。
        var filtered = new List<LogisticsCompany>
        {
            CreateCompany("顺丰速运", "SF", enabled: true)
        };

        _repoMock
            .Setup(r => r.ListAsync(1, 20, "顺丰", (LogisticsCompanyStatus?)null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filtered);

        // Act
        var result = await _sut.ListAsync(1, 20, "顺丰", null);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("顺丰速运");
        result[0].Name.Should().Contain("顺丰");

        _repoMock.Verify(
            r => r.ListAsync(1, 20, "顺丰", (LogisticsCompanyStatus?)null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListAsync_WithStatusEnabled_ShouldReturnOnlyEnabledCompanies()
    {
        // Arrange：仓储按 status=Enabled 过滤后仅返回 2 条 Enabled 记录。
        var enabled = new List<LogisticsCompany>
        {
            CreateCompany("顺丰速运", "SF", enabled: true),
            CreateCompany("圆通速递", "YT", enabled: true)
        };

        _repoMock
            .Setup(r => r.ListAsync(1, 20, (string?)null, LogisticsCompanyStatus.Enabled, It.IsAny<CancellationToken>()))
            .ReturnsAsync(enabled);

        // Act
        var result = await _sut.ListAsync(1, 20, null, LogisticsCompanyStatus.Enabled);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(c => c.Status == LogisticsCompanyStatus.Enabled).Should().BeTrue();

        _repoMock.Verify(
            r => r.ListAsync(1, 20, (string?)null, LogisticsCompanyStatus.Enabled, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListAsync_WithKeywordAndStatus_ShouldReturnCombinedFilterResult()
    {
        // Arrange：仓储按 keyword="顺丰" 与 status=Enabled 组合过滤后仅返回 1 条匹配记录。
        var combined = new List<LogisticsCompany>
        {
            CreateCompany("顺丰速运", "SF", enabled: true)
        };

        _repoMock
            .Setup(r => r.ListAsync(1, 20, "顺丰", LogisticsCompanyStatus.Enabled, It.IsAny<CancellationToken>()))
            .ReturnsAsync(combined);

        // Act
        var result = await _sut.ListAsync(1, 20, "顺丰", LogisticsCompanyStatus.Enabled);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("顺丰速运");
        result[0].Name.Should().Contain("顺丰");
        result[0].Status.Should().Be(LogisticsCompanyStatus.Enabled);

        _repoMock.Verify(
            r => r.ListAsync(1, 20, "顺丰", LogisticsCompanyStatus.Enabled, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListAsync_EmptyResult_ShouldReturnEmptyList()
    {
        // Arrange：仓储返回空集合（无匹配记录），服务层应返回空列表而非 null。
        _repoMock
            .Setup(r => r.ListAsync(1, 20, "不存在", LogisticsCompanyStatus.Disabled, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LogisticsCompany>());

        // Act
        var result = await _sut.ListAsync(1, 20, "不存在", LogisticsCompanyStatus.Disabled);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _repoMock.Verify(
            r => r.ListAsync(1, 20, "不存在", LogisticsCompanyStatus.Disabled, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// 工厂辅助方法：构造一个物流公司聚合根，可指定启停状态。
    /// 默认 SupportTracking=true、ServicePhone="10000"，由测试按需调整 Name/Code/enabled。
    /// </summary>
    private static LogisticsCompany CreateCompany(string name, string code, bool enabled)
    {
        var company = LogisticsCompany.Create(Guid.NewGuid(), name, code, "10000", true);
        if (!enabled)
        {
            company.Disable();
        }
        return company;
    }
}
