namespace Leno.SharedContracts.Integration.Dto;

/// <summary>
/// 积分冻结结果共享 DTO（D2.5 ACL 模式去重）。
/// 各 BC 的 PointsAntiCorruptionService.FreezeAsync 统一返回此类型，消除 Order / Promotion / ReviewAfterSales 3 BC 重复定义。
/// 现有 Order BC 实现返回 void，本 DTO 为未来丰富返回值预留超集；当前 BC 迁移期可仅填充 Success 字段。
/// </summary>
public sealed class PointsFreezeResultDto
{
    /// <summary>订单标识（积分冻结的关联业务单据）。</summary>
    public Guid OrderId { get; init; }

    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>本次冻结的积分数。</summary>
    public int PointsFrozen { get; init; }

    /// <summary>用户冻结后剩余可用积分（不含已冻结）。</summary>
    public int RemainingPoints { get; init; }

    /// <summary>冻结积分可抵现的金额。</summary>
    public decimal OffsetAmount { get; init; }

    /// <summary>币种（ISO 4217，如 "CNY"）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>冻结时间（UTC）。</summary>
    public DateTime FrozenAt { get; init; }

    /// <summary>操作是否成功。</summary>
    public bool Success { get; init; } = true;

    /// <summary>失败原因码（成功时为空字符串）。</summary>
    public string FailureCode { get; init; } = string.Empty;
}

/// <summary>
/// 积分确认扣减结果共享 DTO（D2.5 ACL 模式去重）。
/// 支付成功后将冻结积分转为正式扣减时返回此结果。
/// </summary>
public sealed class PointsConfirmResultDto
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>本次确认扣减的积分数。</summary>
    public int PointsConfirmed { get; init; }

    /// <summary>用户扣减后剩余可用积分。</summary>
    public int RemainingPoints { get; init; }

    /// <summary>确认扣减时间（UTC）。</summary>
    public DateTime ConfirmedAt { get; init; }

    /// <summary>操作是否成功。</summary>
    public bool Success { get; init; } = true;

    /// <summary>失败原因码（成功时为空字符串）。</summary>
    public string FailureCode { get; init; } = string.Empty;
}

/// <summary>
/// 积分释放结果共享 DTO（D2.5 ACL 模式去重）。
/// 订单取消时释放冻结积分返回此结果。
/// </summary>
public sealed class PointsReleaseResultDto
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>本次释放的积分数。</summary>
    public int PointsReleased { get; init; }

    /// <summary>用户释放后剩余可用积分。</summary>
    public int RemainingPoints { get; init; }

    /// <summary>释放时间（UTC）。</summary>
    public DateTime ReleasedAt { get; init; }

    /// <summary>操作是否成功。</summary>
    public bool Success { get; init; } = true;

    /// <summary>失败原因码（成功时为空字符串）。</summary>
    public string FailureCode { get; init; } = string.Empty;

    /// <summary>是否为幂等返回（订单无冻结记录或已释放）。</summary>
    public bool IsIdempotentReturn { get; init; }
}
