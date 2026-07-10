using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Aggregates;

/// <summary>
/// 秒杀活动聚合根，封装限时限量特价活动的不变量。
/// 高频预扣由 <c>ISeckillStockService</c> 在 Redis 原子完成（库存 + 限购），
/// 本聚合持有 DB 权威基线，每次成功预扣后由应用层调用 <see cref="DeductStock"/> 同步基线。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>ActivityId</c>。
/// </summary>
public sealed class SeckillActivity : AggregateRoot
{
    /// <summary>关联商品 SPU 标识。</summary>
    public Guid SpuId { get; private set; }

    /// <summary>关联商品 SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>秒杀价，须 &lt; 原价。</summary>
    public decimal SeckillPrice { get; private set; }

    /// <summary>原价（用于展示划线价与校验）。</summary>
    public decimal OriginalPrice { get; private set; }

    /// <summary>总库存。</summary>
    public int TotalStock { get; private set; }

    /// <summary>当前可用库存（DB 基线，Redis 为高频权威值）。</summary>
    public int AvailableStock { get; private set; }

    /// <summary>每人限购数量，&gt; 0。限购校验由 Redis Lua 原子完成。</summary>
    public int LimitPerUser { get; private set; }

    /// <summary>活动开始时间（UTC）。</summary>
    public DateTime StartTime { get; private set; }

    /// <summary>活动结束时间（UTC），须晚于开始时间。</summary>
    public DateTime EndTime { get; private set; }

    /// <summary>活动状态。</summary>
    public SeckillStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private SeckillActivity() { }

    private SeckillActivity(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验秒杀价 &lt; 原价、库存 &gt; 0、时间合法，置待生效态。
    /// </summary>
    /// <param name="activityId">活动标识，由应用层生成。</param>
    /// <param name="spuId">SPU 标识。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="seckillPrice">秒杀价。</param>
    /// <param name="originalPrice">原价。</param>
    /// <param name="totalStock">总库存，须 &gt; 0。</param>
    /// <param name="limitPerUser">每人限购，须 &gt; 0。</param>
    /// <param name="startTime">开始时间（UTC）。</param>
    /// <param name="endTime">结束时间（UTC）。</param>
    public static SeckillActivity Create(
        Guid activityId,
        Guid spuId,
        Guid skuId,
        decimal seckillPrice,
        decimal originalPrice,
        int totalStock,
        int limitPerUser,
        DateTime startTime,
        DateTime endTime)
    {
        if (spuId == Guid.Empty)
        {
            throw new PromotionDomainException("SpuId 不可为空", "SECKILL_SPU_EMPTY");
        }

        if (skuId == Guid.Empty)
        {
            throw new PromotionDomainException("SkuId 不可为空", "SECKILL_SKU_EMPTY");
        }

        if (seckillPrice <= 0)
        {
            throw new PromotionDomainException("秒杀价须大于 0", "SECKILL_PRICE_INVALID");
        }

        if (originalPrice <= 0)
        {
            throw new PromotionDomainException("原价须大于 0", "SECKILL_ORIGINAL_PRICE_INVALID");
        }

        if (seckillPrice >= originalPrice)
        {
            throw new PromotionDomainException("秒杀价须小于原价", "SECKILL_PRICE_GE_ORIGINAL");
        }

        if (totalStock <= 0)
        {
            throw new PromotionDomainException("总库存须大于 0", "SECKILL_STOCK_INVALID");
        }

        if (limitPerUser <= 0)
        {
            throw new PromotionDomainException("每人限购须大于 0", "SECKILL_LIMIT_INVALID");
        }

        if (endTime <= startTime)
        {
            throw new PromotionDomainException("活动结束时间须晚于开始时间", "SECKILL_TIME_INVALID");
        }

        return new SeckillActivity(activityId == Guid.Empty ? Guid.NewGuid() : activityId)
        {
            SpuId = spuId,
            SkuId = skuId,
            SeckillPrice = seckillPrice,
            OriginalPrice = originalPrice,
            TotalStock = totalStock,
            AvailableStock = totalStock,
            LimitPerUser = limitPerUser,
            StartTime = startTime,
            EndTime = endTime,
            Status = SeckillStatus.Pending
        };
    }

    /// <summary>
    /// 激活活动，仅 Pending 态可激活为 Active。
    /// </summary>
    public void Activate()
    {
        if (Status != SeckillStatus.Pending)
        {
            throw new PromotionDomainException(
                $"当前状态 {Status} 不可激活，仅 Pending 可激活",
                "SECKILL_ACTIVATE_INVALID");
        }

        Status = SeckillStatus.Active;
    }

    /// <summary>
    /// 关闭活动，非 Closed 态均可关闭为终态 Closed。
    /// </summary>
    public void Close()
    {
        if (Status == SeckillStatus.Closed)
        {
            throw new PromotionDomainException("活动已关闭，不可重复关闭", "SECKILL_CLOSED");
        }

        Status = SeckillStatus.Closed;
    }

    /// <summary>
    /// 同步扣减 DB 库存基线（Redis 预扣成功后由应用层调用）。
    /// 校验活动进行中、库存充足；扣减后若售罄则置 Ended。
    /// 限购校验由 Redis 原子完成（<c>ISeckillStockService</c>），本方法不重复校验。
    /// </summary>
    /// <param name="userId">下单用户标识（用于审计与事件关联）。</param>
    /// <param name="quantity">扣减数量，须 &gt; 0。</param>
    public void DeductStock(Guid userId, int quantity)
    {
        if (userId == Guid.Empty)
        {
            throw new PromotionDomainException("UserId 不可为空", "SECKILL_USER_EMPTY");
        }

        if (quantity <= 0)
        {
            throw new PromotionDomainException("扣减数量须大于 0", "SECKILL_DEDUCT_QTY_INVALID");
        }

        EnsureActive();

        if (AvailableStock < quantity)
        {
            throw new PromotionDomainException(
                $"秒杀库存不足：可用 {AvailableStock}，本次 {quantity}",
                "SECKILL_STOCK_INSUFFICIENT");
        }

        AvailableStock -= quantity;

        if (AvailableStock == 0)
        {
            Status = SeckillStatus.Ended;
        }
    }

    /// <summary>
    /// 秒杀订单取消回退库存。回退后可用库存不可超过总库存。
    /// </summary>
    /// <param name="quantity">回退数量，须 &gt; 0。</param>
    public void RestoreStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new PromotionDomainException("回退数量须大于 0", "SECKILL_RESTORE_QTY_INVALID");
        }

        if (Status == SeckillStatus.Closed)
        {
            throw new PromotionDomainException("活动已关闭，不可回退库存", "SECKILL_RESTORE_CLOSED");
        }

        var restored = AvailableStock + quantity;
        if (restored > TotalStock)
        {
            throw new PromotionDomainException(
                $"回退后库存 {restored} 超过总库存 {TotalStock}",
                "SECKILL_RESTORE_EXCEED");
        }

        AvailableStock = restored;

        // 售罄后回退库存，恢复为进行中（若仍在时间区间内）
        if (Status == SeckillStatus.Ended && IsWithinActiveWindow(DateTime.UtcNow))
        {
            Status = SeckillStatus.Active;
        }
    }

    /// <summary>判断当前时间是否在活动有效区间内。</summary>
    public bool IsWithinActiveWindow(DateTime now)
        => now >= StartTime && now < EndTime;

    private void EnsureActive()
    {
        if (Status != SeckillStatus.Active)
        {
            throw new PromotionDomainException(
                $"当前状态 {Status} 不可下单，仅 Active 可下单",
                "SECKILL_NOT_ACTIVE");
        }

        if (!IsWithinActiveWindow(DateTime.UtcNow))
        {
            throw new PromotionDomainException("活动不在有效时间区间内", "SECKILL_OUT_OF_TIME");
        }
    }
}
