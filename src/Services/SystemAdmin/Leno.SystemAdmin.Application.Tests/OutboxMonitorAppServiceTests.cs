using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Application.Tests;

/// <summary>
/// Outbox 监控应用服务单元测试，覆盖汇总、趋势、事件查询、重投、归档、归档历史用例与参数校验。
/// </summary>
public class OutboxMonitorAppServiceTests
{
    private readonly Mock<IOutboxQueryService> _queryServiceMock = new();
    private readonly Mock<IOutboxArchiveRecordRepository> _archiveRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly OutboxMonitorAppService _sut;

    private const string Context = "Order";
    private const string OperatorId = "op-001";

    public OutboxMonitorAppServiceTests()
    {
        _sut = new OutboxMonitorAppService(
            _queryServiceMock.Object,
            _archiveRepoMock.Object,
            _uowMock.Object,
            NullLogger<OutboxMonitorAppService>.Instance);
    }

    #region GetSummaryAsync

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnSummaries()
    {
        var summaries = new List<OutboxContextSummary>
        {
            new()
            {
                Context = "Order",
                PendingCount = 10,
                OldestPendingAt = DateTime.UtcNow.AddMinutes(-30),
                MaxAgeMinutes = 30,
                Status = OutboxContextStatus.Backlog
            },
            new()
            {
                Context = "Payment",
                PendingCount = 0,
                OldestPendingAt = null,
                MaxAgeMinutes = 0,
                Status = OutboxContextStatus.Normal
            }
        };
        _queryServiceMock
            .Setup(s => s.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);

        var result = await _sut.GetSummaryAsync();

        result.Should().HaveCount(2);
        result[0].Context.Should().Be("Order");
        result[0].PendingCount.Should().Be(10);
        result[0].Status.Should().Be(OutboxContextStatus.Backlog);
        result[1].Context.Should().Be("Payment");
        result[1].Status.Should().Be(OutboxContextStatus.Normal);
    }

    [Fact]
    public async Task GetSummaryAsync_EmptyResult_ShouldReturnEmptyList()
    {
        _queryServiceMock
            .Setup(s => s.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxContextSummary>());

        var result = await _sut.GetSummaryAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region GetTrendAsync

    [Fact]
    public async Task GetTrendAsync_ShouldReturnTrendPoints()
    {
        var points = new List<OutboxTrendPoint>
        {
            new() { Timestamp = DateTime.UtcNow.AddHours(-1), Context = "Order", PendingCount = 5 },
            new() { Timestamp = DateTime.UtcNow, Context = "Order", PendingCount = 10 }
        };
        _queryServiceMock
            .Setup(s => s.GetTrendAsync(24, It.IsAny<CancellationToken>()))
            .ReturnsAsync(points);

        var result = await _sut.GetTrendAsync();

        result.Should().HaveCount(2);
        result[0].PendingCount.Should().Be(5);
        result[1].PendingCount.Should().Be(10);
    }

    [Fact]
    public async Task GetTrendAsync_WithZeroHours_ShouldNormalizeTo24()
    {
        _queryServiceMock
            .Setup(s => s.GetTrendAsync(24, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxTrendPoint>());

        await _sut.GetTrendAsync(0);

        _queryServiceMock.Verify(s => s.GetTrendAsync(24, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrendAsync_WithNegativeHours_ShouldNormalizeTo24()
    {
        _queryServiceMock
            .Setup(s => s.GetTrendAsync(24, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxTrendPoint>());

        await _sut.GetTrendAsync(-5);

        _queryServiceMock.Verify(s => s.GetTrendAsync(24, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrendAsync_WithTooLargeHours_ShouldClampTo168()
    {
        _queryServiceMock
            .Setup(s => s.GetTrendAsync(168, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxTrendPoint>());

        await _sut.GetTrendAsync(500);

        _queryServiceMock.Verify(s => s.GetTrendAsync(168, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetMessagesAsync

    [Fact]
    public async Task GetMessagesAsync_ShouldReturnPaginatedMessages()
    {
        var message = OutboxMessageEntry.Create(
            Guid.NewGuid(), Context, Guid.NewGuid(), "OrderCreatedIntegrationEvent",
            "{\"orderId\":\"123\"}", "Pending", 0, null, DateTime.UtcNow.AddMinutes(-30), null);
        _queryServiceMock
            .Setup(s => s.GetMessagesAsync(Context, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxMessageQueryResult { Items = new List<OutboxMessageEntry> { message }, Total = 1 });

        var result = await _sut.GetMessagesAsync(Context, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Items[0].MessageId.Should().Be(message.Id);
        result.Items[0].EventType.Should().Be("OrderCreatedIntegrationEvent");
        result.Items[0].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task GetMessagesAsync_ShouldTrimContext()
    {
        _queryServiceMock
            .Setup(s => s.GetMessagesAsync(Context, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxMessageQueryResult { Items = new List<OutboxMessageEntry>(), Total = 0 });

        await _sut.GetMessagesAsync("  Order  ", null, 1, 20);

        _queryServiceMock.Verify(s => s.GetMessagesAsync(Context, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessagesAsync_ShouldNormalizePageAndPageSize()
    {
        _queryServiceMock
            .Setup(s => s.GetMessagesAsync(Context, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxMessageQueryResult { Items = new List<OutboxMessageEntry>(), Total = 0 });

        var result = await _sut.GetMessagesAsync(Context, null, page: 0, pageSize: 0);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetMessagesAsync_WithEmptyContext_ShouldThrow()
    {
        var act = () => _sut.GetMessagesAsync("", null, 1, 20);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*限界上下文不可为空*");
        _queryServiceMock.Verify(s => s.GetMessagesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMessagesAsync_WithTooLongContext_ShouldThrow()
    {
        var ctx = new string('c', 129);
        var act = () => _sut.GetMessagesAsync(ctx, null, 1, 20);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*限界上下文长度不可超过 128*");
        _queryServiceMock.Verify(s => s.GetMessagesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region RepublishAsync

    [Fact]
    public async Task RepublishAsync_WithMessageIds_ShouldReturnResult()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var serviceResult = new OutboxRepublishResult
        {
            SuccessCount = 2,
            FailureCount = 0,
            Errors = new List<OutboxRepublishError>()
        };
        _queryServiceMock
            .Setup(s => s.RepublishAsync(Context, It.Is<IReadOnlyCollection<Guid>>(c => c.SequenceEqual(ids)), OperatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _sut.RepublishAsync(Context, ids, OperatorId);

        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task RepublishAsync_WithNullMessageIds_ShouldRepublishAll()
    {
        _queryServiceMock
            .Setup(s => s.RepublishAsync(Context, null, OperatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxRepublishResult { SuccessCount = 100, FailureCount = 0, Errors = new List<OutboxRepublishError>() });

        var result = await _sut.RepublishAsync(Context, null, OperatorId);

        result.SuccessCount.Should().Be(100);
        _queryServiceMock.Verify(s => s.RepublishAsync(Context, null, OperatorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepublishAsync_WithEmptyMessageIds_ShouldRepublishAll()
    {
        _queryServiceMock
            .Setup(s => s.RepublishAsync(Context, null, OperatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxRepublishResult { SuccessCount = 50, FailureCount = 0, Errors = new List<OutboxRepublishError>() });

        var result = await _sut.RepublishAsync(Context, new List<Guid>(), OperatorId);

        result.SuccessCount.Should().Be(50);
        _queryServiceMock.Verify(s => s.RepublishAsync(Context, null, OperatorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepublishAsync_WithDuplicateIds_ShouldDeduplicate()
    {
        var id = Guid.NewGuid();
        var ids = new List<Guid> { id, id, id };
        _queryServiceMock
            .Setup(s => s.RepublishAsync(Context, It.Is<IReadOnlyCollection<Guid>>(c => c.Count() == 1 && c.First() == id), OperatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxRepublishResult { SuccessCount = 1, FailureCount = 0, Errors = new List<OutboxRepublishError>() });

        var result = await _sut.RepublishAsync(Context, ids, OperatorId);

        result.SuccessCount.Should().Be(1);
    }

    [Fact]
    public async Task RepublishAsync_WithEmptyContext_ShouldThrow()
    {
        var act = () => _sut.RepublishAsync("", null, OperatorId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*限界上下文不可为空*");
        _queryServiceMock.Verify(s => s.RepublishAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepublishAsync_WithEmptyOperatorId_ShouldThrow()
    {
        var act = () => _sut.RepublishAsync(Context, null, "");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*操作者标识不可为空*");
        _queryServiceMock.Verify(s => s.RepublishAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region ArchiveAsync

    [Fact]
    public async Task ArchiveAsync_Valid_ShouldReturnArchiveResult()
    {
        var before = DateTime.UtcNow.AddHours(-24);
        _queryServiceMock
            .Setup(s => s.ArchiveAsync(Context, before, OperatorId, "陈旧清理", It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);

        var result = await _sut.ArchiveAsync(Context, before, OperatorId, "陈旧清理");

        result.ArchivedCount.Should().Be(15);
        result.RecordId.Should().NotBe(Guid.Empty);
        _archiveRepoMock.Verify(r => r.AddAsync(It.IsAny<OutboxArchiveRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_WithZeroArchivedCount_ShouldStillRecordHistory()
    {
        var before = DateTime.UtcNow.AddHours(-24);
        _queryServiceMock
            .Setup(s => s.ArchiveAsync(Context, before, OperatorId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.ArchiveAsync(Context, before, OperatorId, "无陈旧事件");

        result.ArchivedCount.Should().Be(0);
        result.RecordId.Should().NotBe(Guid.Empty);
        _archiveRepoMock.Verify(r => r.AddAsync(It.IsAny<OutboxArchiveRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_WithEmptyContext_ShouldThrow()
    {
        var before = DateTime.UtcNow.AddHours(-24);
        var act = () => _sut.ArchiveAsync("", before, OperatorId, "原因");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*限界上下文不可为空*");
        _queryServiceMock.Verify(s => s.ArchiveAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _archiveRepoMock.Verify(r => r.AddAsync(It.IsAny<OutboxArchiveRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveAsync_WithEmptyOperatorId_ShouldThrow()
    {
        var before = DateTime.UtcNow.AddHours(-24);
        var act = () => _sut.ArchiveAsync(Context, before, "", "原因");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*操作者标识不可为空*");
        _queryServiceMock.Verify(s => s.ArchiveAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _archiveRepoMock.Verify(r => r.AddAsync(It.IsAny<OutboxArchiveRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveAsync_WithEmptyReason_ShouldThrow()
    {
        var before = DateTime.UtcNow.AddHours(-24);
        var act = () => _sut.ArchiveAsync(Context, before, OperatorId, "");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*归档原因不可为空*");
        _queryServiceMock.Verify(s => s.ArchiveAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _archiveRepoMock.Verify(r => r.AddAsync(It.IsAny<OutboxArchiveRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveAsync_WithTooLongReason_ShouldThrow()
    {
        var before = DateTime.UtcNow.AddHours(-24);
        var reason = new string('r', 1001);
        var act = () => _sut.ArchiveAsync(Context, before, OperatorId, reason);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*归档原因长度不可超过 1000*");
        _queryServiceMock.Verify(s => s.ArchiveAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveAsync_WithDefaultBefore_ShouldThrow()
    {
        var act = () => _sut.ArchiveAsync(Context, default, OperatorId, "原因");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*归档阈值必须为过去的有效时间*");
        _queryServiceMock.Verify(s => s.ArchiveAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveAsync_WithFutureBefore_ShouldThrow()
    {
        var future = DateTime.UtcNow.AddHours(1);
        var act = () => _sut.ArchiveAsync(Context, future, OperatorId, "原因");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*归档阈值必须为过去的有效时间*");
        _queryServiceMock.Verify(s => s.ArchiveAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GetArchiveHistoryAsync

    [Fact]
    public async Task GetArchiveHistoryAsync_ShouldReturnPaginatedHistory()
    {
        var record = OutboxArchiveRecord.Create(
            Guid.NewGuid(), Context, 10, DateTime.UtcNow.AddHours(-24),
            DateTime.UtcNow, OperatorId, "陈旧清理");
        _archiveRepoMock
            .Setup(r => r.QueryAsync(Context, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxArchiveRecord> { record });
        _archiveRepoMock
            .Setup(r => r.CountAsync(Context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.GetArchiveHistoryAsync(Context, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Items[0].RecordId.Should().Be(record.Id);
        result.Items[0].Context.Should().Be(Context);
        result.Items[0].ArchivedCount.Should().Be(10);
        result.Items[0].ArchivedBy.Should().Be(OperatorId);
    }

    [Fact]
    public async Task GetArchiveHistoryAsync_ShouldNormalizePage()
    {
        _archiveRepoMock
            .Setup(r => r.QueryAsync(Context, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxArchiveRecord>());
        _archiveRepoMock
            .Setup(r => r.CountAsync(Context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.GetArchiveHistoryAsync(Context, page: 0, pageSize: 0);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetArchiveHistoryAsync_WithEmptyContext_ShouldThrow()
    {
        var act = () => _sut.GetArchiveHistoryAsync("", 1, 20);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*限界上下文不可为空*");
        _archiveRepoMock.Verify(r => r.QueryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
