namespace Leno.Inventory.Domain.Services;

/// <summary>
/// 秒杀库存 Redis 预扣服务接口，封装高频秒杀场景下的原子库存与限购校验。
/// 实现位于基础设施层，基于 Redis Hash 与 Lua 脚本保证"扣减库存 + 校验限购"原子性。
/// 秒杀下单时由应用层先调用 <see cref="TryDeductAsync"/>，成功后再异步创建订单。
/// </summary>
/// <remarks>
/// 秒杀结算收口后的现状（2026-09-24 复核）：本接口与实现**当前无调用方** —— 秒杀配额仍由 Promotion BC 的
/// <c>RedisSeckillStockService</c> 承担（准入控制）；而库存权威（SQL 台账）的写入已落在 Order 的秒杀建单
/// （<c>SeckillOrderCreationService</c> → <c>IInventoryGateway.ReserveBatchAsync</c>）。
/// 即：本接口是"秒杀配额迁入 Inventory"这一未完成迁移的残留实现，是否保留/删除待单独决策。
/// </remarks>
public interface ISeckillStockService
{
    /// <summary>
    /// 初始化秒杀活动 Redis 库存（活动激活时调用），支持多 SKU。
    /// 使用 Hash 结构：<c>seckill:{activityId}:stock</c>，field = skuId。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="skuStocks">SKU 标识到库存数量的映射。</param>
    Task InitializeAsync(Guid activityId, Dictionary<Guid, int> skuStocks, CancellationToken ct = default);

    /// <summary>
    /// 原子预扣秒杀库存：校验指定 SKU 库存充足与每人限购，成功则扣减并累加用户已购数量。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="userId">下单用户标识。</param>
    /// <param name="quantity">本次下单数量，须 &gt; 0。</param>
    /// <param name="limitPerUser">每人限购上限。</param>
    /// <returns>0=成功，1=库存不足，2=超出用户限购上限。</returns>
    Task<int> TryDeductAsync(
        Guid activityId,
        Guid skuId,
        Guid userId,
        int quantity,
        int limitPerUser,
        CancellationToken ct = default);

    /// <summary>
    /// 回退秒杀库存（订单取消时调用），回退指定 SKU 库存。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="quantity">回退数量，须 &gt; 0。</param>
    Task RestoreAsync(
        Guid activityId,
        Guid skuId,
        int quantity,
        CancellationToken ct = default);

    /// <summary>
    /// 查询秒杀活动指定 SKU 当前 Redis 剩余库存。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="skuId">SKU 标识。</param>
    Task<int> GetAvailableAsync(Guid activityId, Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 获取活动所有 SKU 的 Redis 库存快照。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <returns>SKU 标识到剩余库存的映射。</returns>
    Task<Dictionary<Guid, int>> GetAllStocksAsync(Guid activityId, CancellationToken ct = default);
}
