namespace Leno.Points.Application.DTOs;

/// <summary>
/// 试算积分抵扣结果 DTO，返回可抵扣金额与使用的积分数量。
/// 由订单域在下单预览时调用积分域 internal 端点获取。
/// </summary>
public sealed class TrialOffsetResultDto
{
    /// <summary>实际可抵扣金额（元），不超过订单金额。</summary>
    public decimal OffsetAmount { get; init; }

    /// <summary>本次试算使用的积分数量，不超过用户可用余额。</summary>
    public int UsedPoints { get; init; }

    /// <summary>币种，默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";
}

/// <summary>
/// 冻结积分结果 DTO，返回冻结成功状态与冻结明细。
/// 由订单域在下单时调用积分域 internal 端点预占积分。
/// </summary>
public sealed class FreezeResultDto
{
    /// <summary>是否冻结成功。</summary>
    public bool Success { get; init; }

    /// <summary>本次冻结的积分数量。</summary>
    public int Points { get; init; }

    /// <summary>触发冻结的订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>积分账户标识。</summary>
    public Guid AccountId { get; init; }

    /// <summary>冻结后可用余额。</summary>
    public int AvailableBalanceAfter { get; init; }

    /// <summary>冻结后冻结余额。</summary>
    public int FrozenBalanceAfter { get; init; }
}
