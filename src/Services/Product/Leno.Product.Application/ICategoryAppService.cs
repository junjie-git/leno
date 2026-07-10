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

    /// <summary>查询分类树（仅启用分类，按层级与排序组装）。</summary>
    Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>按标识查询分类。</summary>
    Task<CategoryDto> GetByIdAsync(Guid categoryId, CancellationToken ct = default);
}
