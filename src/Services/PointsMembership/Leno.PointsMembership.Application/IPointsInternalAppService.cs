namespace Leno.PointsMembership.Application;

/// <summary>
/// 积分域内部操作服务，供订单域调用以试扣、冻结、释放积分。
/// </summary>
public interface IPointsInternalAppService
{
    /// <summary>
    /// 试算积分可抵扣金额（如 100 积分 = 1 元），不修改账户状态。
    /// </summary>
    /// <param name="input">试扣参数。</param>
    Task<TrialOffsetResultDto> TrialOffsetAsync(TrialOffsetDto input, CancellationToken ct = default);

    /// <summary>
    /// 冻结积分（下单预占），校验余额充足。
    /// </summary>
    /// <param name="input">冻结参数。</param>
    Task FreezeAsync(FreezePointsDto input, CancellationToken ct = default);

    /// <summary>
    /// 释放冻结积分（订单取消回退）。
    /// </summary>
    /// <param name="input">释放参数。</param>
    Task ReleaseAsync(ReleasePointsDto input, CancellationToken ct = default);
}

/// <summary>
/// 试扣积分入参 DTO。
/// </summary>
public sealed class TrialOffsetDto
{
    public Guid UserId { get; set; }

    public int PointsToUse { get; set; }
}

/// <summary>
/// 试扣积分结果 DTO，返回可抵扣金额与币种。
/// </summary>
public sealed class TrialOffsetResultDto
{
    public decimal OffsetAmount { get; set; }

    public string Currency { get; set; } = "CNY";
}

/// <summary>
/// 冻结积分入参 DTO。
/// </summary>
public sealed class FreezePointsDto
{
    public Guid UserId { get; set; }

    public Guid OrderId { get; set; }

    public int PointsToUse { get; set; }
}

/// <summary>
/// 释放冻结积分入参 DTO。
/// </summary>
public sealed class ReleasePointsDto
{
    public Guid OrderId { get; set; }
}
