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

    /// <summary>
    /// 原子创建匿名购物车（Redis SET NX）：仅当 Key 不存在时写入。
    /// 用于 <c>GetOrCreateCartAsync</c> 并发场景，避免两个请求同时遇 null 都创建并覆盖后者丢失。
    /// </summary>
    /// <param name="sessionId">会话标识。</param>
    /// <param name="cart">待创建的匿名购物车聚合。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// <c>true</c> 表示成功创建（Key 之前不存在，已写入）；
    /// <c>false</c> 表示 Key 已存在（并发请求已创建），调用方应重新 <see cref="GetAsync"/> 读取。
    /// </returns>
    Task<bool> TrySaveAsync(string sessionId, CartAggregate cart, CancellationToken ct = default);

    /// <summary>删除匿名购物车。</summary>
    Task RemoveAsync(string sessionId, CancellationToken ct = default);

    /// <summary>刷新匿名购物车 TTL。</summary>
    Task RefreshTtlAsync(string sessionId, CancellationToken ct = default);
}