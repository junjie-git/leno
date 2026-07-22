using Leno.Cart.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Domain.Repositories;

/// <summary>
/// 购物车仓储接口，以 UserId 为唯一键管理购物车聚合。
/// </summary>
public interface ICartRepository : IRepository<CartAggregate>
{
    /// <summary>按买家标识加载购物车（含全部购物车项）。</summary>
    Task<CartAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 按买家标识加载购物车（含全部购物车项），只读路径使用 <c>AsNoTracking</c> 不跟踪实体。
    /// 供查询/展示/结算预览等只读场景使用，避免 ChangeTracker 无谓跟踪；写路径仍使用 <see cref="GetByUserIdAsync"/>。
    /// </summary>
    Task<CartAggregate?> GetByUserIdReadOnlyAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 按购物车标识加载购物车（含全部购物车项），只读路径使用 <c>AsNoTracking</c> 不跟踪实体。
    /// </summary>
    Task<CartAggregate?> GetByIdReadOnlyAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 批量按购物车标识加载购物车（含全部购物车项）。
    /// 用于商品事件消费者批量处理受影响购物车，避免 N+1 查询（替代 foreach + GetByIdAsync）。
    /// 返回的实体由 ChangeTracker 跟踪，调用方可直接修改后经 UnitOfWork 保存。
    /// </summary>
    /// <param name="cartIds">购物车标识集合。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>命中的购物车聚合集合；未命中的标识不在结果中。</returns>
    Task<IReadOnlyList<CartAggregate>> GetByIdsAsync(IReadOnlyCollection<Guid> cartIds, CancellationToken ct = default);
}
