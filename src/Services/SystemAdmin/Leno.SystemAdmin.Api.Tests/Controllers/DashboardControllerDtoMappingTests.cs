using Leno.SystemAdmin.Api.Controllers;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Api.Tests.Controllers;

/// <summary>
/// 验证 <see cref="DashboardController"/> 端点返回 DTO 而非领域聚合 <see cref="DashboardReport"/>，
/// 确保 M-01 修复后聚合内部结构不对外泄露。
/// </summary>
public sealed class DashboardControllerDtoMappingTests
{
    private readonly Mock<IStatisticsAggregationService> _aggregationMock = new();
    private readonly Mock<IDashboardReportRepository> _reportRepoMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly DashboardController _controller;

    public DashboardControllerDtoMappingTests()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        _currentUserMock.SetupGet(c => c.Role).Returns("Admin");

        _controller = new DashboardController(
            _currentUserMock.Object,
            _aggregationMock.Object,
            _reportRepoMock.Object);
    }

    [Fact]
    public async Task GetOverviewAsync_Should_Return_DashboardReportDto_Not_Domain_Aggregate()
    {
        var report = BuildSampleReport(ReportType.OrderGmv);
        _aggregationMock
            .Setup(s => s.AggregateAsync(ReportType.OrderGmv, It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var result = await _controller.GetOverviewAsync(null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<DashboardReportDto>>(ok.Value);
        Assert.NotNull(response.Data);
        Assert.Equal(report.ReportId, response.Data!.ReportId);
        Assert.Equal(ReportType.OrderGmv, response.Data.ReportType);
        Assert.Equal(report.Granularity, response.Data.Granularity);
        Assert.Equal(report.Metrics.Count, response.Data.Metrics.Count);
        Assert.Equal(report.Period.Start, response.Data.PeriodStart);
        Assert.Equal(report.Period.End, response.Data.PeriodEnd);
    }

    [Fact]
    public async Task GetReportsAsync_Should_Return_List_Of_DashboardReportDto()
    {
        var report1 = BuildSampleReport(ReportType.OrderGmv);
        var report2 = BuildSampleReport(ReportType.PaymentSuccessRate);
        _reportRepoMock
            .Setup(r => r.GetByPeriodAsync(It.IsAny<ReportType>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardReport> { report1, report2 });

        var result = await _controller.GetReportsAsync(ReportType.OrderGmv, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<List<DashboardReportDto>>>(ok.Value);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data!.Count);
    }

    [Fact]
    public async Task GetReportByIdAsync_Should_Return_Dto_When_Found()
    {
        var report = BuildSampleReport(ReportType.PointsIssued);
        _reportRepoMock
            .Setup(r => r.GetByIdAsync(report.ReportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var result = await _controller.GetReportByIdAsync(report.ReportId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<DashboardReportDto>>(ok.Value);
        Assert.Equal(report.ReportId, response.Data!.ReportId);
    }

    [Fact]
    public async Task GetReportByIdAsync_Should_Return_404_When_Not_Found()
    {
        _reportRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DashboardReport?)null);

        var result = await _controller.GetReportByIdAsync(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
    }

    private static DashboardReport BuildSampleReport(ReportType reportType)
    {
        var period = new ReportPeriod(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        var metrics = new List<MetricItem>
        {
            new("total_orders", 100m, "单"),
            new("total_gmv", 10000m, "CNY")
        };
        return DashboardReport.Create(Guid.NewGuid(), reportType, period, metrics, "daily");
    }
}
