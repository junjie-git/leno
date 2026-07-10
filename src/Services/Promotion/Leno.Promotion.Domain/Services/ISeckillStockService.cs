namespace Leno.Promotion.Domain.Services;

/// <summary>
/// 秒杀库存 Redis 预扣服务接口，封装高频秒杀场景下的原子库存与限购校验。
/// 实现位于基础设施层，基于 Redis Lua 脚本保证“扣减库存 + 校验限购”原子性。
/// 秒杀下单时由应用层先调用 <see cref="TryDeductAsync"/>，成功后再异步创建订单。
/// </summary>
public interface ISeckillStockService
{
    /// <summary>
    /// 初始化秒杀活动 Redis 库存（活动激活时调用）。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="totalStock">总库存。</param>
    Task InitializeAsync(Guid activityId, int totalStock, CancellationToken ct = default);

    /// <summary>
    /// 原子预扣秒杀库存：校验库存充足与每人限购，成功则扣减并累加用户已购数量。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="userId">下单用户标识。</param>
    /// <param name="quantity">本次下单数量，须 &gt; 0。</param>
    /// <param name="limitPerUser">每人限购上限。</param>
    /// <returns>预扣成功返回 true；库存不足或超限返回 false。</returns>
    Task<bool> TryDeductAsync(
        Guid activityId,
        Guid userId,
        int quantity,
        int limitPerUser,
        CancellationToken ct = default);

    /// <summary>
    /// 回退秒杀库存（订单取消时调用），同步回退库存与用户已购数量。
    /// </summary>
    /// <param name="activityId">秒杀活动标识。</param>
    /// <param name="userId">下单用户标识。</param>
    /// <param name="quantity">回退数量，须 &gt; 0。</param>
    Task RestoreAsync(
        Guid activityId,
        Guid userId,
        int quantity,
        CancellationToken ct = default);

    /// <summary>
    /// 查询秒杀活动当前 Redis 剩余库存。
    /// </summary>
    Task<int> GetAvailableAsync(Guid activityId, CancellationToken ct = default);
}
