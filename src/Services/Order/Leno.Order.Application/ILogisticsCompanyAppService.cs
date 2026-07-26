using Leno.Order.Application.DTOs;
using Leno.Order.Domain.ValueObjects;

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

    /// <summary>分页查询物流公司列表（无筛选，向后兼容旧调用）。</summary>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<LogisticsCompanyDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 分页查询物流公司列表（带关键词与状态筛选）。
    /// 新增重载以支持运营端按 Name/Code 模糊搜索与启停状态过滤，旧签名保留以保持向后兼容。
    /// </summary>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    /// <param name="keyword">关键词，非空时按 Name 或 Code 模糊匹配；空或空白表示不筛选。</param>
    /// <param name="status">物流公司状态过滤，null 表示不筛选。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<LogisticsCompanyDto>> ListAsync(int page, int pageSize, string? keyword, LogisticsCompanyStatus? status, CancellationToken ct = default);
}
