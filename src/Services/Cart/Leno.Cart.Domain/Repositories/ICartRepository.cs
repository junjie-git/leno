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
}
