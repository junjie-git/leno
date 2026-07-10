using Leno.PointsMembership.Domain.ValueObjects;

namespace Leno.PointsMembership.Application.DTOs;

/// <summary>
/// 积分账户 DTO，表达用户积分余额与累计统计。
/// </summary>
public sealed class PointsAccountDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public int Balance { get; init; }

    public int FrozenBalance { get; init; }

    public int TotalEarned { get; init; }

    public int TotalSpent { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 积分流水 DTO，记录单笔积分变动的明细。
/// </summary>
public sealed class PointsLedgerDto
{
    public Guid Id { get; init; }

    public Guid AccountId { get; init; }

    public PointsTxType TxType { get; init; }

    public int Amount { get; init; }

    public int BalanceAfter { get; init; }

    public PointsSource Source { get; init; }

    public Guid ReferenceId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public DateTime OccurredAt { get; init; }
}

/// <summary>
/// 签到结果 DTO，返回本次签到记录与奖励积分。
/// </summary>
public sealed class CheckInResultDto
{
    public Guid RecordId { get; init; }

    public Guid UserId { get; init; }

    public DateOnly CheckInDate { get; init; }

    public int ContinuousDays { get; init; }

    public int PointsAwarded { get; init; }
}

/// <summary>
/// 手动发放积分 DTO，供运营后台奖励用户积分。
/// </summary>
public sealed class AwardPointsDto
{
    public Guid UserId { get; init; }

    public int Amount { get; init; }

    public string Reason { get; init; } = string.Empty;
}
