namespace Leno.Order.Domain.Services;

/// <summary>
/// 积分按卖家分摊领域服务接口（P1-T19）。
/// 将"积分抵现按卖家小计比例分摊、尾差归最后一组"的业务规则从应用层下沉到领域服务，
/// 便于未来扩展为按 SKU 分摊或按优惠后金额分摊时仅修改领域服务而非应用层。
/// </summary>
public interface IPointsAllocationService
{
    /// <summary>
    /// 按各卖家小计占比分摊总积分抵现金额，尾差调整至最后一组以保证总和等于 <paramref name="totalPointsOffset"/>。
    /// 零金额卖家分摊为 0；全部卖家金额为 0 时全部归最后一组。
    /// </summary>
    /// <param name="sellerSubtotals">各卖家标识与小计金额映射。</param>
    /// <param name="totalPointsOffset">待分摊的总积分抵现金额，须 ≥ 0。</param>
    /// <returns>各卖家标识与分摊金额列表，顺序与 <paramref name="sellerSubtotals"/> 一致。</returns>
    IReadOnlyList<(Guid SellerId, decimal AllocatedPointsOffset)> AllocateBySellerRatio(
        IReadOnlyDictionary<Guid, decimal> sellerSubtotals,
        decimal totalPointsOffset);
}
