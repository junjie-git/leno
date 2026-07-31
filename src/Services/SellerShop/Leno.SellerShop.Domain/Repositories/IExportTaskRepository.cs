using Leno.SellerShop.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Repositories;

/// <summary>
/// 导出任务仓储接口，定义在领域层，由基础设施层实现。
/// GetByIdAsync 与 AddAsync 由 <see cref="IRepository{T}"/> 提供，此处仅声明导出任务专属查询。
/// </summary>
public interface IExportTaskRepository : IRepository<ExportTask>
{
    /// <summary>按店铺分页查询导出任务（按状态可选过滤，按创建时间倒序）。</summary>
    Task<(IReadOnlyList<ExportTask> Items, int Total)> ListByShopAsync(
        Guid shopId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>查询最早的处理中任务（供后台作业轮询）。</summary>
    Task<ExportTask?> GetOldestProcessingAsync(CancellationToken ct = default);
}
