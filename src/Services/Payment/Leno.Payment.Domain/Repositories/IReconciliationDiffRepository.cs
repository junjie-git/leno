using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Payment.Domain.Repositories;

/// <summary>
/// 对账差异仓储接口，管理 <see cref="ReconciliationDiff"/> 聚合。
/// </summary>
public interface IReconciliationDiffRepository : IRepository<ReconciliationDiff>
{
    /// <summary>
    /// 按对账日期和渠道查询差异列表。
    /// </summary>
    Task<List<ReconciliationDiff>> QueryAsync(
        DateTime? billDate,
        PaymentChannel? channel,
        ReconciliationDiffType? diffType,
        ReconciliationDiffStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// 条件查询差异总数。
    /// </summary>
    Task<int> CountAsync(
        DateTime? billDate,
        PaymentChannel? channel,
        ReconciliationDiffType? diffType,
        ReconciliationDiffStatus? status,
        CancellationToken ct = default);
}