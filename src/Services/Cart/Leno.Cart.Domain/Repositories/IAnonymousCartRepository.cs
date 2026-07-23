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

    /// <summary>
    /// 保存匿名购物车并设置 TTL（向后兼容重载，P1-1 修复后内部走 CAS）。
    /// <para>
    /// 以聚合当前 <see cref="CartAggregate.Revision"/> 作为 expectedVersion 执行 CAS Lua 脚本。
    /// 成功时聚合 <see cref="CartAggregate.Revision"/> 自动递增；并发冲突时抛出
    /// <see cref="Exceptions.CartConcurrencyException"/>，调用方应重新加载后重试。
    /// </para>
    /// </summary>
    Task SaveAsync(string sessionId, CartAggregate cart, CancellationToken ct = default);

    /// <summary>
    /// CAS（Compare-And-Swap）原子保存匿名购物车（P1-1 修复）。
    /// <para>
    /// 通过 Redis Lua 脚本原子执行：先读取 Hash version 字段，与 <paramref name="expectedVersion"/> 比较；
    /// 相等则写入新 payload 并将 version 递增为 expectedVersion + 1，返回 <c>true</c>；
    /// 不相等（并发冲突）返回 <c>false</c>；key 不存在时按首次创建处理（version 设为 expectedVersion + 1）。
    /// </para>
    /// <para>
    /// 保存成功后，聚合的 <see cref="CartAggregate.Revision"/> 会自动递增为新版本号。
    /// </para>
    /// </summary>
    /// <param name="sessionId">会话标识。</param>
    /// <param name="cart">待保存的匿名购物车聚合。</param>
    /// <param name="expectedVersion">
    /// 期望的当前版本号（应为上次 <see cref="GetAsync"/> 加载后聚合的 <see cref="CartAggregate.Revision"/>）。
    /// 新创建的购物车传入 0。
    /// </param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>
    /// <c>true</c> 表示保存成功（版本匹配，已写入并递增）；
    /// <c>false</c> 表示并发冲突（版本不匹配，未写入，调用方应重新加载后重试）。
    /// </returns>
    Task<bool> SaveAsync(string sessionId, CartAggregate cart, int expectedVersion, CancellationToken ct = default);

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