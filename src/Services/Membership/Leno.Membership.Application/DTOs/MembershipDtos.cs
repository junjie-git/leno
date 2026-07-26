using Leno.Membership.Domain.ValueObjects;

namespace Leno.Membership.Application.DTOs;

/// <summary>
/// 会员 DTO，表达用户会员等级、累计消费与成长值。
/// </summary>
public sealed class MemberDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public int CurrentLevel { get; init; }

    public decimal TotalConsumption { get; init; }

    public DateTime JoinedAt { get; init; }

    public DateTime LevelUpgradedAt { get; init; }

    public MemberStatus Status { get; init; }

    public int GrowthValue { get; init; }

    public int CurrentGrowthLevel { get; init; }
}

/// <summary>
/// 会员等级定义 DTO，表达运营配置的成长值等级门槛。
/// </summary>
public sealed class MemberLevelDefinitionDto
{
    public Guid Id { get; init; }

    public int Level { get; init; }

    public string Name { get; init; } = string.Empty;

    public int MinGrowthValue { get; init; }

    public int MaxGrowthValue { get; init; }

    public string Description { get; init; } = string.Empty;

    public int LevelUpBonusPoints { get; init; }

    public LevelDefinitionStatus Status { get; init; }
}

/// <summary>
/// 创建会员等级定义 DTO。
/// </summary>
public sealed class CreateMemberLevelDefinitionDto
{
    public int Level { get; init; }

    public string Name { get; init; } = string.Empty;

    public int MinGrowthValue { get; init; }

    public int MaxGrowthValue { get; init; }

    public string Description { get; init; } = string.Empty;

    public int LevelUpBonusPoints { get; init; }
}

/// <summary>
/// 更新会员等级定义 DTO（等级编号不可改）。
/// </summary>
public sealed class UpdateMemberLevelDefinitionDto
{
    public string Name { get; init; } = string.Empty;

    public int MinGrowthValue { get; init; }

    public int MaxGrowthValue { get; init; }

    public string Description { get; init; } = string.Empty;

    public int LevelUpBonusPoints { get; init; }
}

/// <summary>
/// 会员套餐 DTO，表达运营配置的可购买套餐。
/// </summary>
public sealed class MembershipPackageDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Level { get; init; }

    public decimal Price { get; init; }

    public int DurationDays { get; init; }

    public string Benefits { get; init; } = string.Empty;

    public PackageStatus Status { get; init; }
}

/// <summary>
/// 创建会员套餐 DTO。
/// </summary>
public sealed class CreateMembershipPackageDto
{
    public string Name { get; init; } = string.Empty;

    public int Level { get; init; }

    public decimal Price { get; init; }

    public int DurationDays { get; init; }

    public string Benefits { get; init; } = string.Empty;
}

/// <summary>
/// 更新会员套餐 DTO（等级编号不可改，仅更新名称、价格、时长与权益）。
/// </summary>
public sealed class UpdateMembershipPackageDto
{
    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public int DurationDays { get; init; }

    public string Benefits { get; init; } = string.Empty;
}

/// <summary>
/// 会员套餐订阅结果 DTO，表达买家订阅套餐后产生的待支付订阅意图。
/// 实际订单创建转发至订单域，本 DTO 仅承载订阅意图的快照信息。
/// </summary>
public sealed class SubscriptionResultDto
{
    /// <summary>订阅意图标识（由 Membership 域生成，订单域创建订单时关联）。</summary>
    public Guid SubscriptionId { get; init; }

    /// <summary>订阅用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>订阅的套餐标识。</summary>
    public Guid PackageId { get; init; }

    /// <summary>套餐名称快照。</summary>
    public string PackageName { get; init; } = string.Empty;

    /// <summary>套餐对应会员等级编号快照。</summary>
    public int Level { get; init; }

    /// <summary>套餐价格快照（订单域以此创建支付单）。</summary>
    public decimal Price { get; init; }

    /// <summary>套餐时长（天）快照。</summary>
    public int DurationDays { get; init; }

    /// <summary>订阅状态：Pending（待支付），支付成功后由订单域回调激活。</summary>
    public string Status { get; init; } = "Pending";

    /// <summary>订阅意图创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; init; }
}
