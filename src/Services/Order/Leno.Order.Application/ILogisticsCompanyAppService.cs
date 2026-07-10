using Leno.Order.Application.DTOs;

namespace Leno.Order.Application;

/// <summary>
/// 物流公司应用服务，编排运营端物流公司 CRUD 与启停用例。
/// </summary>
public interface ILogisticsCompanyAppService
{
    /// <summary>创建物流公司。</summary>
    Task<LogisticsCompanyDto> CreateAsync(CreateLogisticsCompanyDto dto, CancellationToken ct = default);

    /// <summary>更新物流公司可编辑字段。</summary>
    Task<LogisticsCompanyDto> UpdateAsync(Guid id, UpdateLogisticsCompanyDto dto, CancellationToken ct = default);

    /// <summary>启用物流公司。</summary>
    Task EnableAsync(Guid id, CancellationToken ct = default);

    /// <summary>停用物流公司。</summary>
    Task DisableAsync(Guid id, CancellationToken ct = default);

    /// <summary>分页查询物流公司列表。</summary>
    Task<List<LogisticsCompanyDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
}
