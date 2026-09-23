using Leno.Inventory.Application.DTOs;

namespace Leno.Inventory.Application.Services;

/// <summary>
/// 秒杀库存应用服务接口，封装高频秒杀场景下的库存预扣/回退/查询用例。
/// </summary>
/// <remarks>
/// 秒杀结算收口后的现状（2026-09-24 复核）：本接口与实现**当前无调用方**（Promotion 仍用自己的 Redis 配额实现）。
/// 秒杀结算已收口在别处：库存权威（SQL 台账）由 Order 的秒杀建单写入
/// （<c>SeckillOrderCreationService</c> → <c>IInventoryGateway.ReserveBatchAsync</c>）。
/// 故本接口属"秒杀配额迁入 Inventory"这一未完成迁移的残留实现，保留/删除待单独决策。
/// </remarks>
public interface ISeckillStockAppService
{
    /// <summary>
    /// 初始化秒杀活动 Redis 库存（活动激活时调用），支持多 SKU。
    /// 幂等：重复调用相同数据会覆盖 Redis Hash（无副作用）。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="skuStocks">SKU 标识到初始库存数量的映射。</param>
    /// <param name="ct">取消令牌。</param>
    Task InitializeAsync(
        Guid activityId,
        Dictionary<Guid, int> skuStocks,
        CancellationToken ct = default);

    /// <summary>
    /// 原子预扣秒杀库存：校验指定 SKU 库存充足与每人限购，成功则扣减并累加用户已购数量。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="userId">下单用户标识。</param>
    /// <param name="quantity">本次下单数量，须 &gt; 0。</param>
    /// <param name="limitPerUser">每人限购上限。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>预扣结果（Code: 0=成功，1=库存不足，2=超限购）。</returns>
    Task<SeckillDeductResult> TryDeductAsync(
        Guid activityId,
        Guid skuId,
        Guid userId,
        int quantity,
        int limitPerUser,
        CancellationToken ct = default);

    /// <summary>
    /// 回退秒杀库存（订单取消时调用），幂等：相同 idempotencyKey 重复调用直接返回。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="quantity">回退数量，须 &gt; 0。</param>
    /// <param name="idempotencyKey">幂等键，相同键重复调用跳过。</param>
    /// <param name="ct">取消令牌。</param>
    Task RestoreAsync(
        Guid activityId,
        Guid skuId,
        int quantity,
        Guid idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// 查询秒杀活动指定 SKU 当前 Redis 剩余库存。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> GetAvailableAsync(Guid activityId, Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 获取活动所有 SKU 的 Redis 库存快照。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>SKU 标识到剩余库存的映射。</returns>
    Task<Dictionary<Guid, int>> GetAllStocksAsync(Guid activityId, CancellationToken ct = default);
}
