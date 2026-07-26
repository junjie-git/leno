using Leno.Product.Application.DTOs;

namespace Leno.Product.Application;

/// <summary>
/// 分类管理应用服务，编排分类树管理与查询用例。
/// </summary>
public interface ICategoryAppService
{
    /// <summary>运营创建分类。</summary>
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default);

    /// <summary>运营更新分类。</summary>
    Task<CategoryDto> UpdateAsync(Guid categoryId, UpdateCategoryDto dto, CancellationToken ct = default);

    /// <summary>运营启用分类。</summary>
    Task EnableAsync(Guid categoryId, CancellationToken ct = default);

    /// <summary>运营停用分类。</summary>
    Task DisableAsync(Guid categoryId, CancellationToken ct = default);

    /// <summary>
    /// 查询分类树（仅启用分类，按层级与排序组装）。
    /// 当 <paramref name="keyword"/> 非空时，只返回名称包含 keyword（不区分大小写）的节点及其所有祖先节点（构建父链）；
    /// 当 <paramref name="keyword"/> 为空时返回完整分类树。
    /// </summary>
    /// <param name="keyword">过滤关键词，可空。</param>
    /// <param name="ct">取消令牌。</param>
    Task<IReadOnlyList<CategoryDto>> GetTreeAsync(string? keyword = null, CancellationToken ct = default);

    /// <summary>按标识查询分类。</summary>
    Task<CategoryDto> GetByIdAsync(Guid categoryId, CancellationToken ct = default);
}
