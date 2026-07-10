using Leno.Product.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Repositories;

/// <summary>
/// 分类仓储接口，定义在领域层，由基础设施层实现。
/// 支持分类树查询与子分类查询，写操作由工作单元统一提交。
/// </summary>
public interface ICategoryRepository : IRepository<Category>
{
    /// <summary>查询全部启用分类（按 Level、SortOrder 排序），供树形结构组装。</summary>
    Task<IReadOnlyList<Category>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>查询指定父分类的直接子分类。</summary>
    Task<IReadOnlyList<Category>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>按名称与父分类判断是否已存在（同级重名校验）。</summary>
    Task<bool> ExistsByNameAsync(string name, Guid? parentId, CancellationToken ct = default);
}
