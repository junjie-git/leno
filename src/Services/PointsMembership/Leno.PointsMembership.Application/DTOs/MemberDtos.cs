using Leno.PointsMembership.Domain.ValueObjects;

namespace Leno.PointsMembership.Application.DTOs;

/// <summary>
/// 会员 DTO，表达用户会员等级与累计消费。
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
}

/// <summary>
/// 会员等级 DTO，表达运营配置的等级定义。
/// </summary>
public sealed class MembershipLevelDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Level { get; init; }

    public decimal MinConsumption { get; init; }

    public decimal DiscountRate { get; init; }

    public MembershipLevelStatus Status { get; init; }
}

/// <summary>
/// 创建会员等级 DTO。
/// </summary>
public sealed class CreateMembershipLevelDto
{
    public string Name { get; init; } = string.Empty;

    public int Level { get; init; }

    public decimal MinConsumption { get; init; }

    public decimal DiscountRate { get; init; }
}

/// <summary>
/// 更新会员等级 DTO（等级编号不可改，仅更新名称、门槛与折扣率）。
/// </summary>
public sealed class UpdateMembershipLevelDto
{
    public string Name { get; init; } = string.Empty;

    public decimal MinConsumption { get; init; }

    public decimal DiscountRate { get; init; }
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
