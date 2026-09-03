using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Application.Abstractions;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Application.Tests.Services;

/// <summary>
/// 验证 <see cref="SystemConfigAppService.GetDistinctGroupsAsync"/> 走 SQL 层 DISTINCT 查询，
/// 不再加载全部配置后内存 Distinct。
/// </summary>
public sealed class SystemConfigGroupsDistinctTests
{
    private readonly Mock<ISystemConfigRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISystemConfigCache> _cacheMock;
    private readonly SystemConfigAppService _service;

    public SystemConfigGroupsDistinctTests()
    {
        _cacheMock = new Mock<ISystemConfigCache>();
        _service = new SystemConfigAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            NullLogger<SystemConfigAppService>.Instance);
    }

    [Fact]
    public async Task GetDistinctGroupsAsync_Should_Call_Repository_GetDistinctGroupsAsync_Once()
    {
        var expectedGroups = new List<string> { "payment", "notification", "order" };
        _repoMock.Setup(r => r.GetDistinctGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGroups);

        var result = await _service.GetDistinctGroupsAsync(CancellationToken.None);

        Assert.Equal(expectedGroups, result);
        _repoMock.Verify(r => r.GetDistinctGroupsAsync(It.IsAny<CancellationToken>()), Times.Once);
        // 不应再调用 QueryAsync 加载全部配置
        _repoMock.Verify(
            r => r.QueryAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Leno.SystemAdmin.Domain.ValueObjects.ConfigStatus?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDistinctGroupsAsync_Should_Return_Empty_List_When_No_Config_Exists()
    {
        _repoMock.Setup(r => r.GetDistinctGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var result = await _service.GetDistinctGroupsAsync(CancellationToken.None);

        Assert.Empty(result);
    }
}
