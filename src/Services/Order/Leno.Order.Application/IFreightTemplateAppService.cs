using Leno.Order.Application.DTOs;

namespace Leno.Order.Application;

/// <summary>
/// 运费模板应用服务，编排卖家运费模板 CRUD、区域规则更新、启停与查询用例。
/// </summary>
public interface IFreightTemplateAppService
{
    /// <summary>创建运费模板（含区域规则）。</summary>
    Task<FreightTemplateDto> CreateAsync(CreateFreightTemplateDto dto, CancellationToken ct = default);

    /// <summary>更新运费模板区域规则（整体替换）。</summary>
    Task<FreightTemplateDto> UpdateRulesAsync(Guid id, UpdateFreightTemplateRulesDto dto, CancellationToken ct = default);

    /// <summary>启用运费模板。</summary>
    Task EnableAsync(Guid id, CancellationToken ct = default);

    /// <summary>停用运费模板。</summary>
    Task DisableAsync(Guid id, CancellationToken ct = default);

    /// <summary>按卖家标识查询运费模板。</summary>
    Task<FreightTemplateDto?> GetBySellerIdAsync(Guid sellerId, CancellationToken ct = default);

    /// <summary>分页查询运费模板列表。</summary>
    Task<List<FreightTemplateDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
}
