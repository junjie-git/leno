using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Aggregates;

/// <summary>
/// 秒杀预占记录，跟踪 Redis 预扣后的履约状态。
/// 补偿任务扫描超时未履约记录并回退库存。
/// </summary>
public sealed class SeckillPreOccupationRecord : AggregateRoot
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; private set; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>下单用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; private set; }

    /// <summary>预占数量。</summary>
    public int Quantity { get; private set; }

    /// <summary>预占时间（UTC）。</summary>
    public DateTime PreOccupiedAt { get; private set; }

    /// <summary>是否已履约（订单创建成功）。</summary>
    public bool IsFulfilled { get; private set; }

    /// <summary>履约时间（UTC）。</summary>
    public DateTime? FulfilledAt { get; private set; }

    /// <summary>是否已回退（补偿）。</summary>
    public bool IsRolledBack { get; private set; }

    /// <summary>回退时间（UTC）。</summary>
    public DateTime? RolledBackAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private SeckillPreOccupationRecord() { }

    private SeckillPreOccupationRecord(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建预占记录。
    /// </summary>
    public static SeckillPreOccupationRecord Create(
        Guid activityId,
        Guid skuId,
        Guid userId,
        Guid orderId,
        int quantity)
    {
        return new SeckillPreOccupationRecord(Guid.NewGuid())
        {
            ActivityId = activityId,
            SkuId = skuId,
            UserId = userId,
            OrderId = orderId,
            Quantity = quantity,
            PreOccupiedAt = DateTime.UtcNow,
            IsFulfilled = false,
            IsRolledBack = false
        };
    }

    /// <summary>标记履约。</summary>
    public void MarkFulfilled()
    {
        if (IsFulfilled)
        {
            return;
        }

        IsFulfilled = true;
        FulfilledAt = DateTime.UtcNow;
    }

    /// <summary>标记回退。</summary>
    public void MarkRolledBack()
    {
        if (IsRolledBack)
        {
            return;
        }

        IsRolledBack = true;
        RolledBackAt = DateTime.UtcNow;
    }
}