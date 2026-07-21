using Leno.Order.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Repositories;

/// <summary>
/// 物流公司仓储接口，管理 <see cref="LogisticsCompany"/> 聚合。
/// 继承 <see cref="IRepository{T}"/> 获得 GetByIdAsync/AddAsync/UpdateAsync/RemoveAsync 基础能力。
/// </summary>
public interface ILogisticsCompanyRepository : IRepository<LogisticsCompany>
{
    /// <summary>
    /// 分页查询物流公司列表。
    /// </summary>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    Task<List<LogisticsCompany>> ListAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 按物流公司编码查询物流公司，不存在返回 null。
    /// 利用 <c>ix_logistics_companies_code</c> 唯一索引精确查询，替代 <see cref="ListAsync"/> 全量加载后 FirstOrDefault 匹配。
    /// </summary>
    /// <param name="code">物流公司编码。</param>
    /// <param name="ct">取消令牌。</param>
    Task<LogisticsCompany?> GetByCodeAsync(string code, CancellationToken ct = default);
}
