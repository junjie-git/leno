using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Repositories;

/// <summary>
/// 品牌仓储接口，定义在领域层，由基础设施层实现。
/// 支持分页查询与状态过滤，写操作由工作单元统一提交。
/// </summary>
public interface IBrandRepository : IRepository<Brand>
{
    /// <summary>
    /// 分页查询品牌列表，支持按状态过滤与关键词模糊匹配。
    /// </summary>
    /// <param name="status">品牌状态过滤，可空表示不限。</param>
    /// <param name="keyword">品牌名称关键词，可空。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数，最大 100。</param>
    Task<(IReadOnlyList<Brand> Items, int Total)> QueryAsync(
        BrandStatus? status = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}
