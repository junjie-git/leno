using Leno.Order.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Repositories;

/// <summary>
/// 运费模板仓储接口，管理 <see cref="FreightTemplate"/> 聚合。
/// 继承 <see cref="IRepository{T}"/> 获得 GetByIdAsync/AddAsync/UpdateAsync/RemoveAsync 基础能力。
/// </summary>
public interface IFreightTemplateRepository : IRepository<FreightTemplate>
{
    /// <summary>
    /// 按卖家标识查询运费模板（每卖家唯一模板）。
    /// </summary>
    /// <param name="sellerId">卖家标识。</param>
    Task<FreightTemplate?> GetBySellerIdAsync(Guid sellerId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询运费模板列表。
    /// </summary>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    Task<List<FreightTemplate>> ListAsync(int page, int pageSize, CancellationToken ct = default);
}
