using Leno.Points.Domain.Events;
using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using PointsFlowAggregate = Leno.Points.Domain.Aggregates.PointsFlow.PointsFlow;

namespace Leno.Points.Domain.Aggregates.PointsAccount;

/// <summary>
/// 积分账户聚合根，封装账户余额、冻结余额与累计统计的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>AccountId</c>。
/// 不变量：<see cref="Balance"/>.Available + <see cref="Balance"/>.Frozen ≥ 0（仅可自余额冻结）。
/// </summary>
public sealed class PointsAccount : AggregateRoot
{
    /// <summary>积分抵扣换算率：100 积分 = 1 元。</summary>
    private const int PointsPerYuan = 100;

    /// <summary>账户所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>积分余额值对象，封装可用/冻结/累计统计。</summary>
    public PointsBalance Balance { get; private set; } = PointsBalance.Zero;

    /// <summary>
    /// 冻结明细集合，按订单跟踪冻结积分，仅经聚合根 Freeze/ConfirmDeduct/Release 维护。
    /// 持久化为聚合子实体集合，故以可赋值 List 暴露给 EF Core，私有 setter 阻止外部整体替换。
    /// </summary>
    public List<FrozenPoints> FrozenEntries { get; private set; } = new();

    /// <summary>
    /// 积分流水集合，记录账户每笔变动明细，由聚合根状态变更方法同事务追加，EF Core 跟踪自动落库。
    /// 私有 setter 阻止外部整体替换，仅可经聚合根方法内 Flows.Add 追加。
    /// </summary>
    public List<PointsFlowAggregate> Flows { get; private set; } = new();

    /// <summary>EF Core 无参构造。</summary>
    private PointsAccount() { }

    private PointsAccount(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验用户标识非空，初始余额为 0。
    /// </summary>
    /// <param name="accountId">账户标识，由应用层生成。</param>
    /// <param name="userId">所属用户标识。</param>
    public static PointsAccount Create(Guid accountId, Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new PointsDomainException("UserId 不可为空", "POINTS_USER_EMPTY");
        }

        return new PointsAccount(accountId == Guid.Empty ? Guid.NewGuid() : accountId)
        {
            UserId = userId,
            Balance = PointsBalance.Zero
        };
    }

    /// <summary>
    /// 获取积分，校验数量 &gt; 0，累加余额与累计获取，发布 <see cref="PointsEarnedDomainEvent"/>。
    /// </summary>
    /// <param name="source">积分来源。</param>
    /// <param name="amount">获取数量，须 &gt; 0。</param>
    /// <param name="reason">获取原因（用于流水审计）。</param>
    public void Earn(PointsSource source, int amount, string reason)
    {
        Balance = Balance.Earn(amount);
        AddDomainEvent(new PointsEarnedDomainEvent(Id, UserId, amount, source.ToString()));
        Flows.Add(PointsFlowAggregate.Create(
            Guid.NewGuid(), Id, PointsTxType.Earn, amount, Balance.Available, source, Guid.Empty, reason, DateTime.UtcNow));
    }

    /// <summary>
    /// 试算积分可抵扣金额（100 积分 = 1 元），不修改账户状态。
    /// 参数非法或余额不足时返回 0。
    /// </summary>
    /// <param name="pointsToUse">拟使用积分数量。</param>
    /// <returns>可抵扣金额（元）。</returns>
    public decimal TryOffset(int pointsToUse)
    {
        if (pointsToUse <= 0 || Balance.Available < pointsToUse)
        {
            return 0;
        }

        return pointsToUse / (decimal)PointsPerYuan;
    }

    /// <summary>
    /// 冻结积分（下单预占），校验数量 &gt; 0、订单非空、余额充足，
    /// 扣减余额、累加冻结余额、登记冻结明细，发布 <see cref="PointsFrozenDomainEvent"/>。
    /// </summary>
    /// <param name="amount">冻结数量，须 &gt; 0。</param>
    /// <param name="orderId">触发冻结的订单标识。</param>
    public void Freeze(int amount, Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new PointsDomainException("OrderId 不可为空", "POINTS_ORDER_EMPTY");
        }

        Balance = Balance.Freeze(amount);
        FrozenEntries.Add(FrozenPoints.Create(orderId, amount));
        AddDomainEvent(new PointsFrozenDomainEvent(Id, UserId, amount, orderId));
        Flows.Add(PointsFlowAggregate.Create(
            Guid.NewGuid(), Id, PointsTxType.Freeze, amount, Balance.Available,
            PointsSource.Offset, orderId, $"冻结-订单{orderId}", DateTime.UtcNow));
    }

    /// <summary>
    /// 确认扣减（支付成功核销冻结），按订单定位冻结明细，
    /// 扣减冻结余额、累加累计消耗，发布 <see cref="PointsConfirmedDomainEvent"/>。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    public void ConfirmDeduct(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new PointsDomainException("OrderId 不可为空", "POINTS_ORDER_EMPTY");
        }

        var entry = FindFrozenEntry(orderId);
        FrozenEntries.Remove(entry);
        Balance = Balance.ConfirmDeduct(entry.Amount);
        AddDomainEvent(new PointsConfirmedDomainEvent(Id, UserId, entry.Amount, orderId));
        Flows.Add(PointsFlowAggregate.Create(
            Guid.NewGuid(), Id, PointsTxType.Consume, entry.Amount, Balance.Available,
            PointsSource.Offset, orderId, $"确认扣减-订单{orderId}", DateTime.UtcNow));
    }

    /// <summary>
    /// 释放冻结（订单取消回退），按订单定位冻结明细，
    /// 扣减冻结余额、回退至余额，发布 <see cref="PointsReleasedDomainEvent"/>。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    public void Release(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new PointsDomainException("OrderId 不可为空", "POINTS_ORDER_EMPTY");
        }

        var entry = FindFrozenEntry(orderId);
        FrozenEntries.Remove(entry);
        Balance = Balance.Release(entry.Amount);
        AddDomainEvent(new PointsReleasedDomainEvent(Id, UserId, entry.Amount, orderId));
        Flows.Add(PointsFlowAggregate.Create(
            Guid.NewGuid(), Id, PointsTxType.Release, entry.Amount, Balance.Available,
            PointsSource.Offset, orderId, $"释放-订单{orderId}", DateTime.UtcNow));
    }

    /// <summary>
    /// 直接消费积分（不经过冻结-确认流程），扣减可用余额，累加累计消耗，发布 <see cref="PointsConsumedDomainEvent"/>。
    /// 校验数量 &gt; 0 且余额充足。
    /// 用于积分兑换优惠券等场景。
    /// </summary>
    /// <param name="amount">消费积分数量，须 &gt; 0。</param>
    /// <param name="referenceId">关联业务标识（如兑换记录 ID）。</param>
    /// <param name="reason">消费原因。</param>
    public void ConsumePoints(int amount, Guid referenceId, string reason)
    {
        Balance = Balance.Consume(amount);
        AddDomainEvent(new PointsConsumedDomainEvent(Id, UserId, amount, referenceId, reason));
        Flows.Add(PointsFlowAggregate.Create(
            Guid.NewGuid(), Id, PointsTxType.Consume, amount, Balance.Available,
            PointsSource.Offset, referenceId, reason, DateTime.UtcNow));
    }

    /// <summary>
    /// 积分扣回（退款/售后扣回已发放积分），允许余额为负（未来收入抵扣）。
    /// 校验数量 &gt; 0，扣减余额（可能为负），累加累计消耗，发布 <see cref="PointsRevertedDomainEvent"/>。
    /// </summary>
    /// <param name="amount">扣回积分数量，须 &gt; 0。</param>
    /// <param name="referenceId">关联业务标识（如退款单 ID）。</param>
    /// <param name="reason">扣回原因。</param>
    public void RevertPoints(int amount, Guid referenceId, string reason)
    {
        Balance = Balance.Revert(amount);
        AddDomainEvent(new PointsRevertedDomainEvent(Id, UserId, amount, referenceId, reason));
        Flows.Add(PointsFlowAggregate.Create(
            Guid.NewGuid(), Id, PointsTxType.Revert, amount, Balance.Available,
            PointsSource.Refund, referenceId, reason, DateTime.UtcNow));
    }

    /// <summary>
    /// 过期积分清理，扣减可用余额，发布 <see cref="PointsExpiredDomainEvent"/>。
    /// 校验数量 &gt; 0 且余额充足。
    /// </summary>
    /// <param name="points">过期积分数量，须 &gt; 0。</param>
    public void ExpirePoints(int points)
    {
        Balance = Balance.Expire(points);
        AddDomainEvent(new PointsExpiredDomainEvent(UserId, points, DateTime.UtcNow));
        Flows.Add(PointsFlowAggregate.Create(
            Guid.NewGuid(), Id, PointsTxType.Expire, points, Balance.Available,
            PointsSource.Activity, Guid.Empty, "积分过期清理", DateTime.UtcNow));
    }

    /// <summary>
    /// 等级提升奖励积分入账（消费 <see cref="MemberLevelBonus"/> 来源）。
    /// 由 Membership BC 发布 <c>MemberLevelChangedIntegrationEvent</c> 后，Points BC 消费端调用此方法。
    /// </summary>
    /// <param name="amount">奖励积分数量，须 &gt; 0。</param>
    /// <param name="newLevel">触发奖励的会员等级编号。</param>
    public void GrantLevelBonus(int amount, int newLevel)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("等级奖励积分数量须大于 0", "POINTS_LEVEL_BONUS_INVALID");
        }

        Balance = Balance.Earn(amount);
        AddDomainEvent(new PointsEarnedDomainEvent(Id, UserId, amount, PointsSource.MemberLevelBonus.ToString()));
        Flows.Add(PointsFlowAggregate.Create(
            Guid.NewGuid(), Id, PointsTxType.Earn, amount, Balance.Available,
            PointsSource.MemberLevelBonus, Guid.Empty, $"等级提升奖励-V{newLevel}", DateTime.UtcNow));
    }

    private FrozenPoints FindFrozenEntry(Guid orderId)
    {
        var entry = FrozenEntries.FirstOrDefault(e => e.OrderId == orderId);
        if (entry is null)
        {
            throw new PointsDomainException(
                $"订单 {orderId} 的冻结记录不存在",
                "POINTS_FROZEN_ENTRY_NOT_FOUND");
        }

        return entry;
    }
}
