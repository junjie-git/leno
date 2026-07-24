using Leno.Points.Domain.Aggregates.PointsAccount;
using Leno.Points.Domain.Aggregates.PointsExchange;
using Leno.Points.Domain.ValueObjects;

namespace Leno.Points.Application.DTOs;

/// <summary>
/// 积分账户响应 DTO。
/// </summary>
public sealed record PointsAccountDto
{
    public Guid AccountId { get; init; }
    public Guid UserId { get; init; }
    public int AvailableBalance { get; init; }
    public int FrozenBalance { get; init; }
    public int TotalEarned { get; init; }
    public int TotalSpent { get; init; }

    public static PointsAccountDto From(PointsAccount account) => new()
    {
        AccountId = account.Id,
        UserId = account.UserId,
        AvailableBalance = account.Balance.Available,
        FrozenBalance = account.Balance.Frozen,
        TotalEarned = account.Balance.TotalEarned,
        TotalSpent = account.Balance.TotalSpent
    };
}

/// <summary>
/// 积分流水响应 DTO。
/// </summary>
public sealed record PointsFlowDto
{
    public Guid FlowId { get; init; }
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
/// 积分获取请求 DTO。
/// </summary>
public sealed record EarnPointsRequestDto
{
    public Guid UserId { get; init; }
    public PointsSource Source { get; init; }
    public int Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// 积分兑换请求 DTO。
/// </summary>
public sealed record ExchangePointsRequestDto
{
    public Guid UserId { get; init; }
    public Guid TargetId { get; init; }
    public ExchangeType Type { get; init; }
    public int PointsRequired { get; init; }
}
