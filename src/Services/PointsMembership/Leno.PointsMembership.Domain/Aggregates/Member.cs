using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Aggregates;

/// <summary>
/// 会员聚合根，封装用户会员等级、累计消费与状态的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>MemberId</c>。
/// </summary>
public sealed class Member : AggregateRoot
{
    /// <summary>会员所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>当前会员等级编号，初始为 1（普通会员）。</summary>
    public int CurrentLevel { get; private set; }

    /// <summary>累计消费金额。</summary>
    public decimal TotalConsumption { get; private set; }

    /// <summary>加入时间（UTC）。</summary>
    public DateTime JoinedAt { get; private set; }

    /// <summary>最近一次等级提升时间（UTC）。</summary>
    public DateTime LevelUpgradedAt { get; private set; }

    /// <summary>会员状态。</summary>
    public MemberStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Member() { }

    private Member(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验用户标识非空，初始等级为 1、状态为 Active。
    /// </summary>
    /// <param name="memberId">会员标识，由应用层生成。</param>
    /// <param name="userId">所属用户标识。</param>
    public static Member Create(Guid memberId, Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new PointsDomainException("UserId 不可为空", "POINTS_USER_EMPTY");
        }

        var now = DateTime.UtcNow;
        return new Member(memberId == Guid.Empty ? Guid.NewGuid() : memberId)
        {
            UserId = userId,
            CurrentLevel = 1,
            TotalConsumption = 0,
            JoinedAt = now,
            LevelUpgradedAt = now,
            Status = MemberStatus.Active
        };
    }

    /// <summary>
    /// 累加消费金额，校验金额 &gt; 0。
    /// </summary>
    /// <param name="amount">消费金额，须 &gt; 0。</param>
    public void AddConsumption(decimal amount)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("消费金额须大于 0", "MEMBER_CONSUMPTION_INVALID");
        }

        TotalConsumption += amount;
    }

    /// <summary>
    /// 等级升级判定，在已配置的等级门槛中命中累计消费满足的最高档，
    /// 若高于当前等级则提升并发布 <see cref="MemberLevelUpgradedEvent"/>。
    /// </summary>
    /// <param name="thresholds">已启用的等级门槛集合。</param>
    public void CheckUpgrade(List<LevelThreshold> thresholds)
    {
        LevelThreshold? matched = null;
        foreach (var threshold in thresholds)
        {
            if (threshold.MinConsumption <= TotalConsumption
                && (matched is null || threshold.Level > matched.Level))
            {
                matched = threshold;
            }
        }

        if (matched is not null && matched.Level > CurrentLevel)
        {
            var oldLevel = CurrentLevel;
            CurrentLevel = matched.Level;
            LevelUpgradedAt = DateTime.UtcNow;
            AddDomainEvent(new MemberLevelUpgradedEvent(UserId, oldLevel, CurrentLevel, LevelUpgradedAt));
        }
    }

    /// <summary>冻结会员，仅 Active 态可冻结。</summary>
    public void Freeze()
    {
        if (Status != MemberStatus.Active)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可冻结，仅 Active 可冻结",
                "MEMBER_FREEZE_INVALID");
        }

        Status = MemberStatus.Frozen;
    }

    /// <summary>解冻会员，仅 Frozen 态可解冻。</summary>
    public void Unfreeze()
    {
        if (Status != MemberStatus.Frozen)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可解冻，仅 Frozen 可解冻",
                "MEMBER_UNFREEZE_INVALID");
        }

        Status = MemberStatus.Active;
    }
}
