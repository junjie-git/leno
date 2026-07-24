using Leno.Points.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.Points.Domain.Aggregates.PointsAccount;

/// <summary>
/// 积分冻结明细实体，隶属于 <see cref="PointsAccount"/> 聚合，记录单笔订单冻结的积分。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>FrozenEntryId</c>。
/// </summary>
public sealed class FrozenPoints : Entity
{
    /// <summary>触发冻结的订单标识。</summary>
    public Guid OrderId { get; private set; }

    /// <summary>冻结积分数量。</summary>
    public int Amount { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private FrozenPoints() { }

    private FrozenPoints(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验订单标识非空、冻结数量 &gt; 0。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="amount">冻结数量，须 &gt; 0。</param>
    public static FrozenPoints Create(Guid orderId, int amount)
    {
        if (orderId == Guid.Empty)
        {
            throw new PointsDomainException("OrderId 不可为空", "POINTS_ORDER_EMPTY");
        }

        if (amount <= 0)
        {
            throw new PointsDomainException("冻结积分数量须大于 0", "POINTS_FREEZE_AMOUNT_INVALID");
        }

        return new FrozenPoints(Guid.NewGuid())
        {
            OrderId = orderId,
            Amount = amount
        };
    }
}
