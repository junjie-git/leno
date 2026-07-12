using Leno.Cart.Domain.Aggregates;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Domain.Repositories;

/// <summary>
/// 匿名购物车仓储接口，以会话标识为键管理匿名购物车聚合。
/// 匿名购物车持久化在 Redis 中，不依赖用户认证。
/// </summary>
public interface IAnonymousCartRepository
{
    /// <summary>按会话标识加载匿名购物车。</summary>
    Task<CartAggregate?> GetAsync(string sessionId, CancellationToken ct = default);

    /// <summary>保存匿名购物车并设置 TTL。</summary>
    Task SaveAsync(string sessionId, CartAggregate cart, CancellationToken ct = default);

    /// <summary>删除匿名购物车。</summary>
    Task RemoveAsync(string sessionId, CancellationToken ct = default);

    /// <summary>刷新匿名购物车 TTL。</summary>
    Task RefreshTtlAsync(string sessionId, CancellationToken ct = default);
}