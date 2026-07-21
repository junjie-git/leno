using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Aggregates;

/// <summary>
/// 积分账户聚合根，封装账户余额、冻结余额与累计统计的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>AccountId</c>。
/// 不变量：<see cref="Balance"/> + <see cref="FrozenBalance"/> ≥ 0（仅可自余额冻结）。
/// </summary>
public sealed class PointsAccount : AggregateRoot
{
    /// <summary>积分抵扣换算率：100 积分 = 1 元。</summary>
    private const int PointsPerYuan = 100;

    /// <summary>账户所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>可用积分余额。</summary>
    public int Balance { get; private set; }

    /// <summary>冻结积分余额（下单预占未核销）。</summary>
    public int FrozenBalance { get; private set; }

    /// <summary>累计获取积分。</summary>
    public int TotalEarned { get; private set; }

    /// <summary>累计消耗积分。</summary>
    public int TotalSpent { get; private set; }

    /// <summary>
    /// 冻结明细集合，按订单跟踪冻结积分，仅经聚合根 Freeze/ConfirmDeduct/Release 维护。
    /// 持久化为聚合子实体集合，故以可赋值 List 暴露给 EF Core，私有 setter 阻止外部整体替换。
    /// </summary>
    public List<PointsFrozenEntry> FrozenEntries { get; private set; } = new();

    /// <summary>
    /// 积分流水集合，记录账户每笔变动明细，由聚合根状态变更方法同事务追加，EF Core 跟踪自动落库。
    /// 私有 setter 阻止外部整体替换，仅可经聚合根方法内 Ledgers.Add 追加。
    /// </summary>
    public List<PointsLedger> Ledgers { get; private set; } = new();

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
            Balance = 0,
            FrozenBalance = 0,
            TotalEarned = 0,
            TotalSpent = 0
        };
    }

    /// <summary>
    /// 获取积分，校验数量 &gt; 0，累加余额与累计获取，发布 <see cref="PointsEarnedEvent"/>。
    /// </summary>
    /// <param name="source">积分来源。</param>
    /// <param name="amount">获取数量，须 &gt; 0。</param>
    /// <param name="reason">获取原因（用于流水审计）。</param>
    public void Earn(PointsSource source, int amount, string reason)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("获取积分数量须大于 0", "POINTS_EARN_AMOUNT_INVALID");
        }

        Balance += amount;
        TotalEarned += amount;
        AddDomainEvent(new PointsEarnedEvent(Id, UserId, amount, source.ToString()));
        Ledgers.Add(PointsLedger.Create(
            Guid.NewGuid(), Id, PointsTxType.Earn, amount, Balance, source, Guid.Empty, reason, DateTime.UtcNow));
    }

    /// <summary>
    /// 试算积分可抵扣金额（100 积分 = 1 元），不修改账户状态。
    /// 参数非法或余额不足时返回 0。
    /// </summary>
    /// <param name="pointsToUse">拟使用积分数量。</param>
    /// <returns>可抵扣金额（元）。</returns>
    public decimal TryOffset(int pointsToUse)
    {
        if (pointsToUse <= 0 || Balance < pointsToUse)
        {
            return 0;
        }

        return pointsToUse / (decimal)PointsPerYuan;
    }

    /// <summary>
    /// 冻结积分（下单预占），校验数量 &gt; 0、订单非空、余额充足，
    /// 扣减余额、累加冻结余额、登记冻结明细，发布 <see cref="PointsFrozenEvent"/>。
    /// </summary>
    /// <param name="amount">冻结数量，须 &gt; 0。</param>
    /// <param name="orderId">触发冻结的订单标识。</param>
    public void Freeze(int amount, Guid orderId)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("冻结积分数量须大于 0", "POINTS_FREEZE_AMOUNT_INVALID");
        }

        if (orderId == Guid.Empty)
        {
            throw new PointsDomainException("OrderId 不可为空", "POINTS_ORDER_EMPTY");
        }

        if (Balance < amount)
        {
            throw new PointsDomainException(
                $"积分余额不足：可用 {Balance}，本次冻结 {amount}",
                "POINTS_BALANCE_INSUFFICIENT");
        }

        Balance -= amount;
        FrozenBalance += amount;
        FrozenEntries.Add(PointsFrozenEntry.Create(orderId, amount));
        AddDomainEvent(new PointsFrozenEvent(Id, UserId, amount, orderId));
        Ledgers.Add(PointsLedger.Create(
            Guid.NewGuid(), Id, PointsTxType.Freeze, amount, Balance,
            PointsSource.Offset, orderId, $"冻结-订单{orderId}", DateTime.UtcNow));
    }

    /// <summary>
    /// 确认扣减（支付成功核销冻结），按订单定位冻结明细，
    /// 扣减冻结余额、累加累计消耗，发布 <see cref="PointsConfirmedEvent"/>。
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
        FrozenBalance -= entry.Amount;
        TotalSpent += entry.Amount;
        AddDomainEvent(new PointsConfirmedEvent(Id, UserId, entry.Amount, orderId));
        Ledgers.Add(PointsLedger.Create(
            Guid.NewGuid(), Id, PointsTxType.Consume, entry.Amount, Balance,
            PointsSource.Offset, orderId, $"确认扣减-订单{orderId}", DateTime.UtcNow));
    }

    /// <summary>
    /// 释放冻结（订单取消回退），按订单定位冻结明细，
    /// 扣减冻结余额、回退至余额，发布 <see cref="PointsReleasedEvent"/>。
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
        FrozenBalance -= entry.Amount;
        Balance += entry.Amount;
        AddDomainEvent(new PointsReleasedEvent(Id, UserId, entry.Amount, orderId));
        Ledgers.Add(PointsLedger.Create(
            Guid.NewGuid(), Id, PointsTxType.Release, entry.Amount, Balance,
            PointsSource.Offset, orderId, $"释放-订单{orderId}", DateTime.UtcNow));
    }

    /// <summary>
    /// 直接消费积分（不经过冻结-确认流程），扣减可用余额，累加累计消耗，发布 <see cref="PointsConsumedEvent"/>。
    /// 校验数量 &gt; 0 且余额充足。
    /// 用于积分兑换优惠券等场景。
    /// </summary>
    /// <param name="amount">消费积分数量，须 &gt; 0。</param>
    /// <param name="referenceId">关联业务标识（如兑换记录 ID）。</param>
    /// <param name="reason">消费原因。</param>
    public void ConsumePoints(int amount, Guid referenceId, string reason)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("消费积分数量须大于 0", "POINTS_CONSUME_AMOUNT_INVALID");
        }

        if (Balance < amount)
        {
            throw new PointsDomainException(
                $"积分余额不足：可用 {Balance}，本次消费 {amount}",
                "POINTS_BALANCE_INSUFFICIENT");
        }

        Balance -= amount;
        TotalSpent += amount;
        AddDomainEvent(new PointsConsumedEvent(Id, UserId, amount, referenceId, reason));
        Ledgers.Add(PointsLedger.Create(
            Guid.NewGuid(), Id, PointsTxType.Consume, amount, Balance,
            PointsSource.Offset, referenceId, reason, DateTime.UtcNow));
    }

    /// <summary>
    /// 积分扣回（退款/售后扣回已发放积分），允许余额为负（未来收入抵扣）。
    /// 校验数量 &gt; 0，扣减余额（可能为负），累加累计消耗，发布 <see cref="PointsRevertedEvent"/>。
    /// </summary>
    /// <param name="amount">扣回积分数量，须 &gt; 0。</param>
    /// <param name="referenceId">关联业务标识（如退款单 ID）。</param>
    /// <param name="reason">扣回原因。</param>
    public void RevertPoints(int amount, Guid referenceId, string reason)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("扣回积分数量须大于 0", "POINTS_REVERT_AMOUNT_INVALID");
        }

        Balance -= amount;
        TotalSpent += amount;
        AddDomainEvent(new PointsRevertedEvent(Id, UserId, amount, referenceId, reason));
        Ledgers.Add(PointsLedger.Create(
            Guid.NewGuid(), Id, PointsTxType.Revert, amount, Balance,
            PointsSource.Refund, referenceId, reason, DateTime.UtcNow));
    }

    /// <summary>
    /// 过期积分清理，扣减可用余额，发布 <see cref="PointsExpiredEvent"/>。
    /// 校验数量 &gt; 0 且余额充足。
    /// </summary>
    /// <param name="points">过期积分数量，须 &gt; 0。</param>
    public void ExpirePoints(int points)
    {
        if (points <= 0)
        {
            throw new PointsDomainException("过期积分数量须大于 0", "POINTS_EXPIRE_AMOUNT_INVALID");
        }

        if (Balance < points)
        {
            throw new PointsDomainException(
                $"积分余额不足：可用 {Balance}，本次过期 {points}",
                "POINTS_EXPIRE_BALANCE_INSUFFICIENT");
        }

        Balance -= points;
        AddDomainEvent(new PointsExpiredEvent(UserId, points, DateTime.UtcNow));
        Ledgers.Add(PointsLedger.Create(
            Guid.NewGuid(), Id, PointsTxType.Expire, points, Balance,
            PointsSource.Activity, Guid.Empty, "积分过期清理", DateTime.UtcNow));
    }

    /// <summary>
    /// 请求兑换优惠券：冻结积分并发起兑换请求领域事件（经 Outbox 翻译为集成事件给优惠券域）。
    /// 同事务保证：冻结成功与事件发布原子性，避免 SaveEntities 提交后再 Publish 失败导致冻结无事件。
    /// </summary>
    /// <param name="amount">兑换所需积分，须 &gt; 0。</param>
    /// <param name="exchangeId">兑换业务标识（同时作为冻结订单 ID）。</param>
    /// <param name="couponTemplateId">兑换目标优惠券模板标识。</param>
    public void RequestExchangeCoupon(int amount, Guid exchangeId, Guid couponTemplateId)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("兑换积分数量须大于 0", "POINTS_EXCHANGE_AMOUNT_INVALID");
        }

        if (exchangeId == Guid.Empty)
        {
            throw new PointsDomainException("ExchangeId 不可为空", "POINTS_EXCHANGE_ID_EMPTY");
        }

        if (couponTemplateId == Guid.Empty)
        {
            throw new PointsDomainException("CouponTemplateId 不可为空", "POINTS_COUPON_TEMPLATE_EMPTY");
        }

        Freeze(amount, exchangeId);
        AddDomainEvent(new PointsExchangeCouponRequestedDomainEvent(
            exchangeId, UserId, couponTemplateId, amount));
    }

    private PointsFrozenEntry FindFrozenEntry(Guid orderId)
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
