using Leno.Cart.Domain.Aggregates;

namespace Leno.Cart.Domain.Repositories;

/// <summary>
/// 匿名购物车合并记录仓储接口。
/// 以 anonymousId 为键查询/插入合并记录，防止跨存储非原子操作导致重复合并。
/// </summary>
public interface ICartMergeRecordRepository
{
    /// <summary>判断指定匿名会话是否已合并。</summary>
    Task<bool> ExistsAsync(string anonymousId, CancellationToken ct = default);

    /// <summary>
    /// 将合并记录加入变更追踪（不立即落库），由 UnitOfWork.SaveEntitiesAsync 统一提交。
    /// 依赖 anonymousId 主键唯一约束：并发插入时第二个事务抛 DbUpdateException。
    /// </summary>
    Task AddAsync(CartMergeRecord record, CancellationToken ct = default);
}
