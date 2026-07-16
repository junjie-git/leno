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

    /// <summary>
    /// 下单时锁定选定优惠券，将 UserCoupon 由 Unused 置为 Locked 并绑定 orderId，
    /// 防止同一优惠券被并发订单重复使用。远程失败（网络异常、非 2xx、超时）抛
    /// <see cref="Leno.Order.Domain.Exceptions.OrderDomainException"/>，由应用层回滚/补偿；用户取消透传 <see cref="OperationCanceledException"/>。
    /// </summary>
    /// <param name="userId">买家标识。</param>
    /// <param name="couponId">优惠券模板标识。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task LockCouponAsync(Guid userId, Guid couponId, Guid orderId, CancellationToken ct = default);
}

/// <summary>
/// 优惠分摊结果，表达优惠总金额按 SKU 的分摊明细。
/// </summary>
public sealed class PromotionDiscountResult
{
    public decimal TotalDiscount { get; set; }

    public List<(Guid SkuId, decimal Allocation)> Allocations { get; set; } = new();
}
