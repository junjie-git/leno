using Leno.Promotion.Domain.Exceptions;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Promotion.Domain.Aggregates;

/// <summary>
/// 满减/促销活动聚合根，封装活动生命周期与满减规则集合的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>ActivityId</c>。
/// </summary>
public sealed class PromotionActivity : AggregateRoot
{
    /// <summary>活动名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>活动类型（满减/优惠券/秒杀）。</summary>
    public PromotionType Type { get; private set; }

    /// <summary>活动状态。</summary>
    public PromotionStatus Status { get; private set; }

    /// <summary>活动开始时间（UTC）。</summary>
    public DateTime StartTime { get; private set; }

    /// <summary>活动结束时间（UTC），须晚于开始时间。</summary>
    public DateTime EndTime { get; private set; }

    /// <summary>
    /// 满减规则集合（按门槛升序，只读视图）。外部不可直接修改，须通过 AddRule/RemoveRule 维护不变量。
    /// 持久化为 JSON 列，EF Core 通过 backing field <c>_rules</c> 反序列化写入。
    /// </summary>
    private readonly List<PromotionRule> _rules = new();
    public IReadOnlyList<PromotionRule> Rules => _rules.AsReadOnly();

    /// <summary>EF Core 无参构造。</summary>
    private PromotionActivity() { }

    private PromotionActivity(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验时间区间并置待生效态。
    /// </summary>
    /// <param name="activityId">活动标识，由应用层生成。</param>
    /// <param name="name">活动名称。</param>
    /// <param name="type">活动类型。</param>
    /// <param name="startTime">开始时间（UTC）。</param>
    /// <param name="endTime">结束时间（UTC）。</param>
    public static PromotionActivity Create(
        Guid activityId,
        string name,
        PromotionType type,
        DateTime startTime,
        DateTime endTime)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PromotionDomainException("活动名称不可为空", "PROMOTION_NAME_EMPTY");
        }

        if (endTime <= startTime)
        {
            throw new PromotionDomainException("活动结束时间须晚于开始时间", "PROMOTION_TIME_INVALID");
        }

        return new PromotionActivity(activityId == Guid.Empty ? Guid.NewGuid() : activityId)
        {
            Name = name,
            Type = type,
            Status = PromotionStatus.Pending,
            StartTime = startTime,
            EndTime = endTime
        };
    }

    /// <summary>
    /// 激活活动，Pending 或 Paused 态可激活为 Active。
    /// </summary>
    public void Activate()
    {
        if (Status != PromotionStatus.Pending && Status != PromotionStatus.Paused)
        {
            throw new PromotionDomainException(
                $"当前状态 {Status} 不可激活，仅 Pending/Paused 可激活",
                "PROMOTION_ACTIVATE_INVALID");
        }

        Status = PromotionStatus.Active;
    }

    /// <summary>
    /// 暂停活动，仅 Active 态可暂停为 Paused。
    /// </summary>
    public void Pause()
    {
        if (Status != PromotionStatus.Active)
        {
            throw new PromotionDomainException(
                $"当前状态 {Status} 不可暂停，仅 Active 可暂停",
                "PROMOTION_PAUSE_INVALID");
        }

        Status = PromotionStatus.Paused;
    }

    /// <summary>
    /// 关闭活动，Pending/Active/Paused 态可关闭为终态 Closed。
    /// </summary>
    public void Close()
    {
        if (Status == PromotionStatus.Closed)
        {
            throw new PromotionDomainException("活动已关闭，不可重复关闭", "PROMOTION_CLOSED");
        }

        Status = PromotionStatus.Closed;
    }

    /// <summary>
    /// 新增满减规则，重复门槛金额视为已存在抛出异常。
    /// </summary>
    /// <param name="thresholdAmount">门槛金额，须 ≥ 0。</param>
    /// <param name="discountAmount">减免金额，须 &gt; 0 且 ≤ 门槛金额。</param>
    public void AddRule(decimal thresholdAmount, decimal discountAmount)
    {
        var rule = new PromotionRule(thresholdAmount, discountAmount);

        if (_rules.Any(r => r.ThresholdAmount == thresholdAmount))
        {
            throw new PromotionDomainException(
                $"门槛金额 {thresholdAmount} 的规则已存在",
                "PROMOTION_RULE_DUPLICATE");
        }

        _rules.Add(rule);
        // 维护按门槛升序，便于命中最高档
        _rules.Sort((a, b) => a.ThresholdAmount.CompareTo(b.ThresholdAmount));
    }

    /// <summary>
    /// 移除指定门槛金额的规则，不存在抛出异常。
    /// </summary>
    public void RemoveRule(decimal thresholdAmount)
    {
        var rule = _rules.FirstOrDefault(r => r.ThresholdAmount == thresholdAmount);
        if (rule is null)
        {
            throw new PromotionDomainException(
                $"门槛金额 {thresholdAmount} 的规则不存在",
                "PROMOTION_RULE_NOT_FOUND");
        }

        _rules.Remove(rule);
    }

    /// <summary>
    /// 按订单金额命中最高档满减规则，返回减免金额；无命中返回 0。
    /// </summary>
    public decimal CalculateDiscount(decimal orderAmount)
    {
        if (Status != PromotionStatus.Active)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        if (now < StartTime || now >= EndTime)
        {
            return 0;
        }

        // 规则已按门槛升序，取满足条件的最高档
        var matched = _rules.LastOrDefault(r => orderAmount >= r.ThresholdAmount);
        return matched?.DiscountAmount ?? 0;
    }
}
