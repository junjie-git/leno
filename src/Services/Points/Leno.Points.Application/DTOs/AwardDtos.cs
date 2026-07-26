namespace Leno.Points.Application.DTOs;

/// <summary>
/// 运营手动发放积分入参 DTO。
/// </summary>
public sealed class AwardPointsDto
{
    /// <summary>目标用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>发放积分数量，须 &gt; 0。</summary>
    public int Amount { get; init; }

    /// <summary>发放原因（用于流水审计）。</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// 运营手动发放积分结果 DTO。
/// </summary>
public sealed class AwardResultDto
{
    /// <summary>积分账户标识。</summary>
    public Guid AccountId { get; init; }

    /// <summary>目标用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>本次发放积分数量。</summary>
    public int Amount { get; init; }

    /// <summary>发放后可用余额。</summary>
    public int AvailableBalanceAfter { get; init; }

    /// <summary>发放后累计获取积分。</summary>
    public int TotalEarnedAfter { get; init; }
}
