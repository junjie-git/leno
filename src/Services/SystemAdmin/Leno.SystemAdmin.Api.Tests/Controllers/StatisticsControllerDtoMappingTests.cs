using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Api.Tests.Controllers;

/// <summary>
/// 验证 <see cref="StatisticsController"/> 端点返回 DTO 而非领域聚合 <see cref="ReconciliationRecord"/>，
/// 确保 M-02 修复后聚合内部结构（Snapshot/AggregatedMetrics/DomainMetrics/Discrepancies 明细）不对外泄露。
/// </summary>
public sealed class StatisticsControllerDtoMappingTests
{
    private readonly Mock<IStatisticsReconciliationService> _reconciliationMock = new();
    private readonly Mock<IReconciliationRecordRepository> _repoMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly StatisticsController _controller;

    public StatisticsControllerDtoMappingTests()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        _currentUserMock.SetupGet(c => c.Role).Returns("Admin");

        _controller = new StatisticsController(
            _currentUserMock.Object,
            _reconciliationMock.Object,
            _repoMock.Object);
    }

    [Fact]
    public async Task TriggerReconciliationAsync_With_ReportType_Should_Return_Dto_Not_Domain_Aggregate()
    {
        var record = BuildSampleRecord(ReportType.OrderGmv, hasDiscrepancy: true);
        _reconciliationMock
            .Setup(s => s.ReconcileAsync(ReportType.OrderGmv, It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var result = await _controller.TriggerReconciliationAsync(ReportType.OrderGmv, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<ReconciliationRecordDto>>(ok.Value);
        Assert.NotNull(response.Data);
        Assert.Equal(record.RecordId, response.Data!.RecordId);
        Assert.Equal(ReportType.OrderGmv, response.Data.ReportType);
        Assert.Equal(record.Snapshot.Discrepancies.Count, response.Data.DiscrepancyCount);
        Assert.Equal(record.AlertTriggered, response.Data.AlertTriggered);
    }

    [Fact]
    public async Task TriggerReconciliationAsync_Without_ReportType_Should_Return_List_Of_Dto()
    {
        var records = new List<ReconciliationRecord>
        {
            BuildSampleRecord(ReportType.OrderGmv, hasDiscrepancy: false),
            BuildSampleRecord(ReportType.PaymentSuccessRate, hasDiscrepancy: true)
        };
        _reconciliationMock
            .Setup(s => s.ReconcileAllAsync(It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var result = await _controller.TriggerReconciliationAsync(null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<List<ReconciliationRecordDto>>>(ok.Value);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data!.Count);
        Assert.Equal(records[0].RecordId, response.Data[0].RecordId);
    }

    [Fact]
    public async Task GetReconciliationRecordsAsync_Should_Return_List_Of_Dto()
    {
        var records = new List<ReconciliationRecord>
        {
            BuildSampleRecord(ReportType.OrderGmv, hasDiscrepancy: true)
        };
        _repoMock
            .Setup(r => r.GetByPeriodAsync(It.IsAny<ReportType>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var result = await _controller.GetReconciliationRecordsAsync(ReportType.OrderGmv, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<List<ReconciliationRecordDto>>>(ok.Value);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data!);
        Assert.Equal(records[0].RecordId, response.Data[0].RecordId);
        Assert.Equal(records[0].Snapshot.Discrepancies.Count, response.Data[0].DiscrepancyCount);
    }

    [Fact]
    public async Task GetReconciliationStatusAsync_Should_Return_StatusDto_When_No_Record_Exists()
    {
        _repoMock
            .Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReconciliationRecord?)null);

        var result = await _controller.GetReconciliationStatusAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<ReconciliationStatusDto>>(ok.Value);
        Assert.False(response.Data!.HasRun);
        Assert.True(response.Data.IsConsistent);
    }

    private static ReconciliationRecord BuildSampleRecord(ReportType reportType, bool hasDiscrepancy)
    {
        var period = new ReportPeriod(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        var aggregated = new List<MetricItem>
        {
            new("total_orders", 100m, "单"),
            new("total_gmv", 10000m, "CNY")
        };
        var domain = new List<MetricItem>
        {
            new("total_orders", hasDiscrepancy ? 99m : 100m, "单"),
            new("total_gmv", hasDiscrepancy ? 9900m : 10000m, "CNY")
        };
        var discrepancies = hasDiscrepancy
            ? new List<MetricDiscrepancy>
            {
                new("total_orders", 100m, 99m),
                new("total_gmv", 10000m, 9900m)
            }
            : new List<MetricDiscrepancy>();

        var snapshot = new StatisticsSnapshot(reportType, period, aggregated, domain, discrepancies);
        var record = ReconciliationRecord.Create(Guid.NewGuid(), snapshot);
        if (hasDiscrepancy)
        {
            record.MarkAlertTriggered();
        }
        return record;
    }
}
