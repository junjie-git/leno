using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Application.Tests;

/// <summary>
/// 告警管理应用服务单元测试，覆盖查询、详情、确认用例与参数规范化、时间范围校验。
/// </summary>
public class AlertAppServiceTests
{
    private readonly Mock<IAlertmanagerClient> _clientMock = new();
    private readonly AlertAppService _sut;

    private static readonly Guid AlertId = Guid.NewGuid();

    public AlertAppServiceTests()
    {
        _sut = new AlertAppService(_clientMock.Object, NullLogger<AlertAppService>.Instance);
    }

    #region QueryAsync

    [Fact]
    public async Task QueryAsync_ShouldReturnPaginatedResult()
    {
        var alert = CreateAlert();
        _clientMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<AlertQueryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertQueryResult { Items = new List<Alert> { alert }, Total = 1 });

        var result = await _sut.QueryAsync("Payment", AlertSeverity.Critical, AlertStatus.Firing, null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items[0].AlertId.Should().Be(alert.Id);
        result.Items[0].Module.Should().Be("Payment");
        _clientMock.Verify(c => c.GetAlertsAsync(It.IsAny<AlertQueryFilter>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_ShouldNormalizePageAndPageSize()
    {
        _clientMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<AlertQueryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertQueryResult { Items = new List<Alert>(), Total = 0 });

        var result = await _sut.QueryAsync(null, null, null, null, null, page: 0, pageSize: 0);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task QueryAsync_ShouldClampPageSizeToMax()
    {
        _clientMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<AlertQueryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertQueryResult { Items = new List<Alert>(), Total = 0 });

        var result = await _sut.QueryAsync(null, null, null, null, null, page: 1, pageSize: 500);

        result.PageSize.Should().Be(200);
    }

    [Fact]
    public async Task QueryAsync_ShouldTrimModuleFilter()
    {
        _clientMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<AlertQueryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertQueryResult { Items = new List<Alert>(), Total = 0 });

        await _sut.QueryAsync("  Payment  ", null, null, null, null, 1, 20);

        _clientMock.Verify(
            c => c.GetAlertsAsync(
                It.Is<AlertQueryFilter>(f => f.Module == "Payment"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WithWhitespaceModule_ShouldNormalizeToNull()
    {
        _clientMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<AlertQueryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertQueryResult { Items = new List<Alert>(), Total = 0 });

        await _sut.QueryAsync("   ", null, null, null, null, 1, 20);

        _clientMock.Verify(
            c => c.GetAlertsAsync(
                It.Is<AlertQueryFilter>(f => f.Module == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WithEndBeforeStart_ShouldThrow()
    {
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddHours(-1);

        var act = () => _sut.QueryAsync(null, null, null, start, end, 1, 20);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*结束时间不可早于起始时间*");
        _clientMock.Verify(c => c.GetAlertsAsync(It.IsAny<AlertQueryFilter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueryAsync_WithRangeExceeding30Days_ShouldThrow()
    {
        var start = DateTime.UtcNow.AddDays(-31);
        var end = DateTime.UtcNow;

        var act = () => _sut.QueryAsync(null, null, null, start, end, 1, 20);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*30*天*");
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_Existing_ShouldReturnDetailDto()
    {
        var alert = CreateAlert();
        _clientMock
            .Setup(c => c.GetAlertAsync(AlertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        var result = await _sut.GetByIdAsync(AlertId);

        result.Should().NotBeNull();
        result!.AlertId.Should().Be(alert.Id);
        result.Name.Should().Be(alert.Name);
        result.Module.Should().Be(alert.Module);
        result.Severity.Should().Be(alert.Severity);
        result.Status.Should().Be(alert.Status);
        result.Labels.Should().BeEquivalentTo(alert.Labels);
        result.Annotations.Should().BeEquivalentTo(alert.Annotations);
    }

    [Fact]
    public async Task GetByIdAsync_NotExisting_ShouldReturnNull()
    {
        _clientMock
            .Setup(c => c.GetAlertAsync(AlertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        var result = await _sut.GetByIdAsync(AlertId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyId_ShouldThrow()
    {
        var act = () => _sut.GetByIdAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*告警标识不可为空*");
        _clientMock.Verify(c => c.GetAlertAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region AcknowledgeAsync

    [Fact]
    public async Task AcknowledgeAsync_ShouldDelegateToClient()
    {
        await _sut.AcknowledgeAsync(AlertId, "op-001", "已介入", CancellationToken.None);

        _clientMock.Verify(
            c => c.AcknowledgeAlertAsync(AlertId, "op-001", "已介入", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcknowledgeAsync_WithEmptyAlertId_ShouldThrow()
    {
        var act = () => _sut.AcknowledgeAsync(Guid.Empty, "op-001", null);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*告警标识不可为空*");
        _clientMock.Verify(c => c.AcknowledgeAlertAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcknowledgeAsync_WithEmptyOperatorId_ShouldThrow()
    {
        var act = () => _sut.AcknowledgeAsync(AlertId, "", null);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*操作者标识不可为空*");
        _clientMock.Verify(c => c.AcknowledgeAlertAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcknowledgeAsync_WithNullComment_ShouldPassNullToClient()
    {
        await _sut.AcknowledgeAsync(AlertId, "op-001", null);

        _clientMock.Verify(
            c => c.AcknowledgeAlertAsync(AlertId, "op-001", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    private static Alert CreateAlert()
        => Alert.Create(
            AlertId,
            "HighErrorRate",
            "Payment",
            AlertSeverity.Critical,
            AlertStatus.Firing,
            new Dictionary<string, string> { ["alertname"] = "HighErrorRate", ["module"] = "Payment" },
            new Dictionary<string, string> { ["summary"] = "错误率超阈值" },
            "payment_error_rate",
            "错误率超阈值",
            "近 5 分钟支付失败率持续超过 5%",
            DateTime.UtcNow.AddMinutes(-10),
            600);
}
