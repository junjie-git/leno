namespace Leno.Order.Application.Services;

/// <summary>
/// 促销域防腐层服务接口，下单时调用促销域计算订单可享优惠金额。
/// 接口定义在应用层，实现位于基础设施层，屏蔽促销域内部模型。
/// </summary>
public interface IPromotionAntiCorruptionService
{
    /// <summary>
    /// 计算订单可享优惠总金额。
    /// </summary>
    /// <param name="userId">买家标识。</param>
    /// <param name="items">参与计算的 SKU 与小计列表。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>优惠总金额。</returns>
    Task<decimal> CalculateDiscountAsync(Guid userId, List<(Guid SkuId, decimal Subtotal)> items, CancellationToken ct = default);

    /// <summary>
    /// 订单取消时释放已使用的优惠券，由促销域根据 orderId 反查并退还。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>
/// 优惠分摊结果，表达优惠总金额按 SKU 的分摊明细。
/// </summary>
public sealed class PromotionDiscountResult
{
    public decimal TotalDiscount { get; set; }

    public List<(Guid SkuId, decimal Allocation)> Allocations { get; set; } = new();
}
