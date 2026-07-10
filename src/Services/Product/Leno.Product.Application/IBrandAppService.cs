using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;

namespace Leno.Product.Application;

/// <summary>
/// 品牌管理应用服务，编排品牌 CRUD 与启停用例。
/// </summary>
public interface IBrandAppService
{
    /// <summary>运营创建品牌。</summary>
    Task<BrandDto> CreateAsync(CreateBrandDto dto, CancellationToken ct = default);

    /// <summary>运营更新品牌。</summary>
    Task<BrandDto> UpdateAsync(Guid brandId, UpdateBrandDto dto, CancellationToken ct = default);

    /// <summary>运营启用品牌。</summary>
    Task EnableAsync(Guid brandId, CancellationToken ct = default);

    /// <summary>运营停用品牌。</summary>
    Task DisableAsync(Guid brandId, CancellationToken ct = default);

    /// <summary>分页查询品牌列表。</summary>
    Task<PageResult<BrandDto>> QueryAsync(BrandQueryDto query, CancellationToken ct = default);

    /// <summary>按标识查询品牌。</summary>
    Task<BrandDto> GetByIdAsync(Guid brandId, CancellationToken ct = default);
}
