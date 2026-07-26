using Leno.Membership.Domain.Exceptions;
using Leno.Membership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Membership.Domain.Aggregates.MemberLevelDefinition;

/// <summary>
/// 会员等级定义聚合根（运营配置），定义 V0-V4 成长值等级门槛与奖励规则。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>MemberLevelDefinitionId</c>。
/// 成长值门槛示例：
///   V0: 0-99
///   V1: 100-499
///   V2: 500-1999
///   V3: 2000-9999
///   V4: 10000+
/// 评估时由应用层加载全部定义并转换为 <see cref="MemberLevel"/> 值对象，传入 <c>Member.EvaluateGrowthLevel</c>。
/// </summary>
public sealed class MemberLevelDefinition : AggregateRoot
{
    /// <summary>等级编号（0-4）。</summary>
    public int Level { get; private set; }

    /// <summary>等级名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>达到该等级所需的最低成长值（含）。</summary>
    public int MinGrowthValue { get; private set; }

    /// <summary>达到该等级所需的最大成长值（不含），0 表示无上限。</summary>
    public int MaxGrowthValue { get; private set; }

    /// <summary>等级描述。</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>等级提升奖励积分数（由 Points BC 消费 MemberLevelChangedIntegrationEvent 时发放）。</summary>
    public int LevelUpBonusPoints { get; private set; }

    /// <summary>等级定义状态，控制是否参与等级评估与展示。</summary>
    public LevelDefinitionStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private MemberLevelDefinition() { }

    private MemberLevelDefinition(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建会员等级定义。
    /// </summary>
    /// <param name="memberLevelId">等级定义标识，由应用层生成。</param>
    /// <param name="level">等级编号（0-4）。</param>
    /// <param name="name">等级名称。</param>
    /// <param name="minGrowthValue">最低成长值（含）。</param>
    /// <param name="maxGrowthValue">最高成长值（不含），0 表示无上限。</param>
    /// <param name="description">等级描述。</param>
    /// <param name="levelUpBonusPoints">等级提升奖励积分。</param>
    public static MemberLevelDefinition Create(
        Guid memberLevelId,
        int level,
        string name,
        int minGrowthValue,
        int maxGrowthValue,
        string description,
        int levelUpBonusPoints = 0)
    {
        Validate(level, name, minGrowthValue, maxGrowthValue);

        if (levelUpBonusPoints < 0)
        {
            throw new MembershipDomainException("等级提升奖励积分不可为负", "MEMBER_LEVEL_BONUS_INVALID");
        }

        return new MemberLevelDefinition(memberLevelId == Guid.Empty ? Guid.NewGuid() : memberLevelId)
        {
            Level = level,
            Name = name,
            MinGrowthValue = minGrowthValue,
            MaxGrowthValue = maxGrowthValue,
            Description = description ?? string.Empty,
            LevelUpBonusPoints = levelUpBonusPoints,
            Status = LevelDefinitionStatus.Enabled
        };
    }

    /// <summary>
    /// 更新等级定义可编辑字段。
    /// </summary>
    /// <param name="name">等级名称。</param>
    /// <param name="minGrowthValue">最低成长值（含）。</param>
    /// <param name="maxGrowthValue">最高成长值（不含），0 表示无上限。</param>
    /// <param name="description">等级描述。</param>
    /// <param name="levelUpBonusPoints">等级提升奖励积分。</param>
    public void Update(
        string name,
        int minGrowthValue,
        int maxGrowthValue,
        string description,
        int levelUpBonusPoints)
    {
        Validate(Level, name, minGrowthValue, maxGrowthValue);

        if (levelUpBonusPoints < 0)
        {
            throw new MembershipDomainException("等级提升奖励积分不可为负", "MEMBER_LEVEL_BONUS_INVALID");
        }

        Name = name;
        MinGrowthValue = minGrowthValue;
        MaxGrowthValue = maxGrowthValue;
        Description = description ?? string.Empty;
        LevelUpBonusPoints = levelUpBonusPoints;
    }

    /// <summary>
    /// 启用等级定义，启用后参与等级评估与买家端展示。
    /// </summary>
    public void Enable()
    {
        if (Status == LevelDefinitionStatus.Enabled)
        {
            throw new MembershipDomainException("会员等级定义已启用", "MEMBER_LEVEL_ALREADY_ENABLED");
        }

        Status = LevelDefinitionStatus.Enabled;
    }

    /// <summary>
    /// 停用等级定义，停用后不参与等级评估，已有会员等级不受影响。
    /// </summary>
    public void Disable()
    {
        if (Status == LevelDefinitionStatus.Disabled)
        {
            throw new MembershipDomainException("会员等级定义已停用", "MEMBER_LEVEL_ALREADY_DISABLED");
        }

        Status = LevelDefinitionStatus.Disabled;
    }

    /// <summary>
    /// 转换为 <see cref="MemberLevel"/> 值对象，供 <c>Member.EvaluateGrowthLevel</c> 使用。
    /// </summary>
    public MemberLevel ToValueObject()
        => new(Level, Name, MinGrowthValue, MaxGrowthValue, Description, LevelUpBonusPoints);

    private static void Validate(int level, string name, int minGrowthValue, int maxGrowthValue)
    {
        if (level < 0 || level > 4)
        {
            throw new MembershipDomainException("等级编号须在 0-4 之间", "MEMBER_LEVEL_INVALID");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MembershipDomainException("等级名称不可为空", "MEMBER_LEVEL_NAME_EMPTY");
        }

        if (minGrowthValue < 0)
        {
            throw new MembershipDomainException("最低成长值不可为负", "MEMBER_LEVEL_MIN_GROWTH_INVALID");
        }

        if (maxGrowthValue < 0)
        {
            throw new MembershipDomainException("最高成长值不可为负", "MEMBER_LEVEL_MAX_GROWTH_INVALID");
        }

        if (maxGrowthValue > 0 && maxGrowthValue <= minGrowthValue)
        {
            throw new MembershipDomainException("最高成长值须大于最低成长值", "MEMBER_LEVEL_RANGE_INVALID");
        }
    }
}
