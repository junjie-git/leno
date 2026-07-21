using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Aggregates;

/// <summary>
/// 会员聚合根，封装用户会员等级、累计消费、成长值与状态的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>MemberId</c>。
/// </summary>
public sealed class Member : AggregateRoot
{
    /// <summary>会员所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>当前会员等级编号（基于消费的等级体系），初始为 1（普通会员）。</summary>
    public int CurrentLevel { get; private set; }

    /// <summary>累计消费金额。</summary>
    public decimal TotalConsumption { get; private set; }

    /// <summary>加入时间（UTC）。</summary>
    public DateTime JoinedAt { get; private set; }

    /// <summary>最近一次等级提升时间（UTC）。</summary>
    public DateTime LevelUpgradedAt { get; private set; }

    /// <summary>会员状态。</summary>
    public MemberStatus Status { get; private set; }

    /// <summary>当前成长值（基于消费积分累计），用于 V0-V4 成长值等级体系。</summary>
    public int GrowthValue { get; private set; }

    /// <summary>最近一次成长值更新时间（UTC）。</summary>
    public DateTime GrowthValueUpdatedAt { get; private set; }

    /// <summary>当前成长值等级编号（V0-V4），初始为 0。</summary>
    public int CurrentGrowthLevel { get; private set; }

    /// <summary>
    /// 等级变更历史记录集合，记录每次成长值等级变化。
    /// 持久化为聚合子实体集合，故以可赋值 List 暴露给 EF Core，私有 setter 阻止外部整体替换。
    /// </summary>
    public List<MemberLevelChangeHistory> LevelChangeHistories { get; private set; } = new();

    /// <summary>EF Core 无参构造。</summary>
    private Member() { }

    private Member(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验用户标识非空，初始等级为 1、成长值等级为 0、状态为 Active。
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
            Status = MemberStatus.Active,
            GrowthValue = 0,
            GrowthValueUpdatedAt = now,
            CurrentGrowthLevel = 0
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

    /// <summary>
    /// 累加成长值，校验数量 &gt; 0。成长值在消费积分入账时同步增加。
    /// PM-M02 修复：将 reason 写入 <see cref="LevelChangeHistories"/> 子实体集合，
    /// 记录每次成长值累加的快照（当前等级、累加后总成长值、原因）。
    /// </summary>
    /// <param name="amount">成长值数量，须 &gt; 0。</param>
    /// <param name="reason">增加原因，写入历史记录。</param>
    public void AddGrowthValue(int amount, string reason)
    {
        if (amount <= 0)
        {
            throw new PointsDomainException("成长值须大于 0", "MEMBER_GROWTH_VALUE_INVALID");
        }

        GrowthValue += amount;
        GrowthValueUpdatedAt = DateTime.UtcNow;

        // PM-M02 修复：记录成长值累加历史（等级未变，仅成长值变动），将 reason 落库
        LevelChangeHistories.Add(new MemberLevelChangeHistory(
            CurrentGrowthLevel, CurrentGrowthLevel, GrowthValue, GrowthValueUpdatedAt, reason));
    }

    /// <summary>
    /// 基于成长值评估等级（V0-V4 体系），若等级变化则发布 <see cref="MemberLevelChangedEvent"/> 并记录变更历史。
    /// </summary>
    /// <param name="levels">已配置的成长值等级定义，按等级编号升序。</param>
    public void EvaluateGrowthLevel(List<MemberLevel> levels)
    {
        var newLevel = MemberLevel.EvaluateLevel(GrowthValue, levels);

        if (newLevel == CurrentGrowthLevel)
        {
            return;
        }

        var oldLevel = CurrentGrowthLevel;
        CurrentGrowthLevel = newLevel;
        var now = DateTime.UtcNow;

        LevelChangeHistories.Add(new MemberLevelChangeHistory(
            oldLevel, newLevel, GrowthValue, now, "成长值评估自动升级"));

        AddDomainEvent(new MemberLevelChangedEvent(UserId, oldLevel, newLevel, GrowthValue));
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
