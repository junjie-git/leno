using Leno.Points.Domain.Exceptions;

namespace Leno.Points.Domain.Aggregates.PointsAccount;

/// <summary>
/// 积分余额值对象，封装可用余额、冻结余额与累计统计的不变量。
/// 不可变，按值相等；所有变更经聚合根方法回写新实例。
/// 不变量：<see cref="Available"/> + <see cref="Frozen"/> ≥ 0。
/// </summary>
public sealed record PointsBalance
{
    /// <summary>可用积分余额。</summary>
    public int Available { get; init; }

    /// <summary>冻结积分余额（下单预占未核销）。</summary>
    public int Frozen { get; init; }

    /// <summary>累计获取积分。</summary>
    public int TotalEarned { get; init; }

    /// <summary>累计消耗积分。</summary>
    public int TotalSpent { get; init; }

    /// <summary>供 EF Core 与反序列化使用的无参构造。</summary>
    public PointsBalance() { }

    public PointsBalance(int available, int frozen, int totalEarned, int totalSpent)
    {
        if (available < 0)
        {
            throw new PointsDomainException("可用积分余额不可为负", "POINTS_BALANCE_NEGATIVE");
        }

        if (frozen < 0)
        {
            throw new PointsDomainException("冻结积分余额不可为负", "POINTS_FROZEN_NEGATIVE");
        }

        if (totalEarned < 0)
        {
            throw new PointsDomainException("累计获取积分不可为负", "POINTS_TOTAL_EARNED_NEGATIVE");
        }

        if (totalSpent < 0)
        {
            throw new PointsDomainException("累计消耗积分不可为负", "POINTS_TOTAL_SPENT_NEGATIVE");
        }

        Available = available;
        Frozen = frozen;
        TotalEarned = totalEarned;
        TotalSpent = totalSpent;
    }

    /// <summary>初始零余额。</summary>
    public static PointsBalance Zero => new(0, 0, 0, 0);

    /// <summary>累加获取积分，返回新余额。</summary>
    public PointsBalance Earn(int amount)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("获取积分数量须大于 0", "POINTS_EARN_AMOUNT_INVALID");
        }

        return this with { Available = Available + amount, TotalEarned = TotalEarned + amount };
    }

    /// <summary>冻结积分，返回新余额。</summary>
    public PointsBalance Freeze(int amount)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("冻结积分数量须大于 0", "POINTS_FREEZE_AMOUNT_INVALID");
        }

        if (Available < amount)
        {
            throw new PointsDomainException(
                $"积分余额不足：可用 {Available}，本次冻结 {amount}",
                "POINTS_BALANCE_INSUFFICIENT");
        }

        return this with { Available = Available - amount, Frozen = Frozen + amount };
    }

    /// <summary>确认扣减冻结，返回新余额。</summary>
    public PointsBalance ConfirmDeduct(int amount)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("扣减积分数量须大于 0", "POINTS_DEDUCT_AMOUNT_INVALID");
        }

        if (Frozen < amount)
        {
            throw new PointsDomainException(
                $"冻结余额不足：当前 {Frozen}，本次扣减 {amount}",
                "POINTS_FROZEN_INSUFFICIENT");
        }

        return this with { Frozen = Frozen - amount, TotalSpent = TotalSpent + amount };
    }

    /// <summary>释放冻结回余额，返回新余额。</summary>
    public PointsBalance Release(int amount)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("释放积分数量须大于 0", "POINTS_RELEASE_AMOUNT_INVALID");
        }

        if (Frozen < amount)
        {
            throw new PointsDomainException(
                $"冻结余额不足：当前 {Frozen}，本次释放 {amount}",
                "POINTS_FROZEN_INSUFFICIENT");
        }

        return this with { Available = Available + amount, Frozen = Frozen - amount };
    }

    /// <summary>直接消费积分（不经过冻结流程），返回新余额。</summary>
    public PointsBalance Consume(int amount)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("消费积分数量须大于 0", "POINTS_CONSUME_AMOUNT_INVALID");
        }

        if (Available < amount)
        {
            throw new PointsDomainException(
                $"积分余额不足：可用 {Available}，本次消费 {amount}",
                "POINTS_BALANCE_INSUFFICIENT");
        }

        return this with { Available = Available - amount, TotalSpent = TotalSpent + amount };
    }

    /// <summary>扣回积分（允许余额为负），返回新余额。</summary>
    public PointsBalance Revert(int amount)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("扣回积分数量须大于 0", "POINTS_REVERT_AMOUNT_INVALID");
        }

        return this with { Available = Available - amount, TotalSpent = TotalSpent + amount };
    }

    /// <summary>过期清理积分，返回新余额。</summary>
    public PointsBalance Expire(int amount)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("过期积分数量须大于 0", "POINTS_EXPIRE_AMOUNT_INVALID");
        }

        if (Available < amount)
        {
            throw new PointsDomainException(
                $"积分余额不足：可用 {Available}，本次过期 {amount}",
                "POINTS_EXPIRE_BALANCE_INSUFFICIENT");
        }

        return this with { Available = Available - amount };
    }
}
