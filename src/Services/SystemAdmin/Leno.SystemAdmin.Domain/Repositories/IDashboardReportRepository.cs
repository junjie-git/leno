using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 运营数据看板报表仓储接口，定义在领域层，由基础设施层实现。
/// 支持按报表类型、时间范围查询。
/// </summary>
public interface IDashboardReportRepository : IRepository<DashboardReport>
{
    /// <summary>
    /// 获取指定类型的最近一份报表。
    /// </summary>
    /// <param name="reportType">报表类型。</param>
    /// <param name="ct">取消令牌。</param>
    Task<DashboardReport?> GetLatestAsync(ReportType reportType, CancellationToken ct = default);

    /// <summary>
    /// 按报表类型和时间范围查询报表列表。
    /// </summary>
    /// <param name="reportType">报表类型。</param>
    /// <param name="start">起始时间（包含）。</param>
    /// <param name="endTime">结束时间（包含）。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<DashboardReport>> GetByPeriodAsync(ReportType reportType, DateTime start, DateTime endTime, CancellationToken ct = default);
}