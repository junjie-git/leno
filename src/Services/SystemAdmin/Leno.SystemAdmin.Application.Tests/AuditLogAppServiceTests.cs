using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.SystemAdmin.Application.Tests;

/// <summary>
/// 审计与操作日志查询应用服务单元测试，覆盖分页查询与 CSV 导出能力。
/// </summary>
public class AuditLogAppServiceTests
{
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock = new();
    private readonly Mock<IOperationLogRepository> _operationLogRepoMock = new();
    private readonly Mock<ILogger<AuditLogAppService>> _loggerMock = new();
    private readonly AuditLogAppService _sut;

    private static readonly Guid OperatorId = Guid.NewGuid();
    private static readonly Guid LogId = Guid.NewGuid();

    public AuditLogAppServiceTests()
    {
        _sut = new AuditLogAppService(
            _auditLogRepoMock.Object,
            _operationLogRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task QueryAuditLogsAsync_ShouldReturnPaginatedResult()
    {
        var logs = new List<AuditLog> { CreateAuditLog() };
        _auditLogRepoMock
            .Setup(r => r.QueryAsync(OperatorId, "Order", null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);
        _auditLogRepoMock
            .Setup(r => r.CountAsync(OperatorId, "Order", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.QueryAuditLogsAsync(OperatorId, "Order", null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items[0].Action.Should().Be("create_order");
        result.Items[0].ResourceType.Should().Be("Order");
        _auditLogRepoMock.Verify(r => r.QueryAsync(OperatorId, "Order", null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAuditLogsAsync_NoMatch_ShouldReturnEmpty()
    {
        _auditLogRepoMock
            .Setup(r => r.QueryAsync(null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLog>());
        _auditLogRepoMock
            .Setup(r => r.CountAsync(null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.QueryAuditLogsAsync(null, null, null, null, 1, 20);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task QueryOperationLogsAsync_ShouldReturnPaginatedResult()
    {
        var logs = new List<OperationLog> { CreateOperationLog() };
        _operationLogRepoMock
            .Setup(r => r.QueryAsync(OperatorId, "SystemConfig", null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);
        _operationLogRepoMock
            .Setup(r => r.CountAsync(OperatorId, "SystemConfig", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.QueryOperationLogsAsync(OperatorId, "SystemConfig", null, null, 1, 10);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Items[0].Module.Should().Be("SystemConfig");
        result.Items[0].OperationType.Should().Be("update");
    }

    [Fact]
    public async Task ExportAuditLogsAsync_ShouldReturnCsvWithHeaderAndRows()
    {
        var logs = new List<AuditLog> { CreateAuditLog() };
        _auditLogRepoMock
            .Setup(r => r.StreamAsync(null, null, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(logs));

        var csv = await _sut.ExportAuditLogsAsync(null, null, null, null);

        csv.Should().StartWith("LogId,OperatorId,Action,ResourceType,ResourceId,ResponseStatus,OccurredAt");
        csv.Should().Contain("create_order");
        csv.Should().Contain("Order");
        csv.Split('\n').Should().HaveCountGreaterOrEqualTo(3); // header + row + trailing newline
    }

    [Fact]
    public async Task ExportAuditLogsAsync_EmptyResult_ShouldReturnHeaderOnly()
    {
        _auditLogRepoMock
            .Setup(r => r.StreamAsync(null, null, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new List<AuditLog>()));

        var csv = await _sut.ExportAuditLogsAsync(null, null, null, null);

        csv.Should().StartWith("LogId,OperatorId,Action,ResourceType,ResourceId,ResponseStatus,OccurredAt");
        csv.Trim().Should().NotContain(Environment.NewLine);
    }

    [Fact]
    public async Task ExportAuditLogsAsync_FieldWithComma_ShouldBeEscaped()
    {
        var log = AuditLog.Create(
            LogId, OperatorId, "create,order", "Order", "ORD-1", "summary", 200, "127.0.0.1", "trace-1", DateTime.UtcNow);
        _auditLogRepoMock
            .Setup(r => r.StreamAsync(null, null, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new List<AuditLog> { log }));

        var csv = await _sut.ExportAuditLogsAsync(null, null, null, null);

        csv.Should().Contain("\"create,order\"");
    }

    [Fact]
    public async Task ExportAuditLogsAsync_ShouldCapAt100000RowsAndLogWarning()
    {
        // 模拟数据源返回超过 10 万条，验证导出截断为 10 万条并记录告警
        var largeLogs = Enumerable.Range(0, 100_001)
            .Select(_ => CreateAuditLog())
            .ToList();
        _auditLogRepoMock
            .Setup(r => r.StreamAsync(null, null, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(largeLogs));

        var csv = await _sut.ExportAuditLogsAsync(null, null, null, null);

        // 表头 + 100000 行 + 末尾换行
        csv.Split('\n').Should().HaveCountGreaterOrEqualTo(100_002);
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("已达到上限", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static async IAsyncEnumerable<AuditLog> CreateAsyncEnumerable(List<AuditLog> logs)
    {
        foreach (var log in logs)
        {
            yield return log;
            await Task.Yield();
        }
    }

    private static AuditLog CreateAuditLog() =>
        AuditLog.Create(
            LogId, OperatorId, "create_order", "Order", "ORD-12345",
            "request summary", 200, "127.0.0.1", "trace-abc", DateTime.UtcNow);

    private static OperationLog CreateOperationLog() =>
        OperationLog.Create(
            LogId, OperatorId, "update", "SystemConfig", "更新支付配置",
            "{\"key\":\"old\"}", "{\"key\":\"new\"}", "127.0.0.1", DateTime.UtcNow);
}
