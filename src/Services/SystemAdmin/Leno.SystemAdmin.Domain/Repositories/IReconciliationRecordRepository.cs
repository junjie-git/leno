using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 对账记录仓储接口，定义在领域层，由基础设施层实现。
/// </summary>
public interface IReconciliationRecordRepository : IRepository<ReconciliationRecord>
{
    /// <summary>
    /// 获取最近一次对账记录。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    Task<ReconciliationRecord?> GetLatestAsync(CancellationToken ct = default);

    /// <summary>
    /// 按报表类型获取最近一次对账记录。
    /// </summary>
    /// <param name="reportType">报表类型。</param>
    /// <param name="ct">取消令牌。</param>
    Task<ReconciliationRecord?> GetLatestByTypeAsync(ReportType reportType, CancellationToken ct = default);

    /// <summary>
    /// 按报表类型和时间范围查询对账记录列表。
    /// </summary>
    /// <param name="reportType">报表类型。</param>
    /// <param name="start">起始时间（包含）。</param>
    /// <param name="endTime">结束时间（包含）。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<ReconciliationRecord>> GetByPeriodAsync(ReportType reportType, DateTime start, DateTime endTime, CancellationToken ct = default);
}