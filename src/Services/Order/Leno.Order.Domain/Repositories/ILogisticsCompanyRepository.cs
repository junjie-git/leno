using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Repositories;

/// <summary>
/// 物流公司仓储接口，管理 <see cref="LogisticsCompany"/> 聚合。
/// 继承 <see cref="IRepository{T}"/> 获得 GetByIdAsync/AddAsync/UpdateAsync/RemoveAsync 基础能力。
/// </summary>
public interface ILogisticsCompanyRepository : IRepository<LogisticsCompany>
{
    /// <summary>
    /// 分页查询物流公司列表（无筛选，向后兼容旧调用）。
    /// </summary>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<LogisticsCompany>> ListAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 分页查询物流公司列表（带关键词与状态筛选）。
    /// 新增重载以支持运营端按 Name/Code 模糊搜索与启停状态过滤，旧签名保留以保持向后兼容。
    /// </summary>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    /// <param name="keyword">关键词，非空时按 Name 或 Code Contains 模糊匹配；空或空白表示不筛选。</param>
    /// <param name="status">物流公司状态过滤，null 表示不筛选。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<LogisticsCompany>> ListAsync(int page, int pageSize, string? keyword, LogisticsCompanyStatus? status, CancellationToken ct = default);

    /// <summary>
    /// 按物流公司编码查询物流公司，不存在返回 null。
    /// 利用 <c>ix_logistics_companies_code</c> 唯一索引精确查询，替代 <see cref="ListAsync"/> 全量加载后 FirstOrDefault 匹配。
    /// </summary>
    /// <param name="code">物流公司编码。</param>
    /// <param name="ct">取消令牌。</param>
    Task<LogisticsCompany?> GetByCodeAsync(string code, CancellationToken ct = default);
}
