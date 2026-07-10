namespace Leno.Order.Domain.Services;

/// <summary>
/// 订单定价领域服务接口，封装下单时的价格校验与优惠分摊计算。
/// 价格校验防篡改：下单单价须与商品域当前售价一致；优惠分摊按小计比例分摊到各明细。
/// </summary>
public interface IOrderPricingDomainService
{
    /// <summary>
    /// 校验下单单价与商品域当前售价一致，不一致抛出领域异常（防篡改）。
    /// </summary>
    /// <param name="skuPrices">SKU 与期望单价列表。</param>
    /// <param name="ct">取消令牌。</param>
    Task ValidatePricesAsync(List<(Guid SkuId, decimal ExpectedPrice)> skuPrices, CancellationToken ct = default);

    /// <summary>
    /// 计算优惠总金额按小计比例分摊到各 SKU，返回各 SKU 分摊金额列表。
    /// 分摊和等于 totalDiscount，尾差调整至最大小计项以保证精确。
    /// </summary>
    /// <param name="totalDiscount">优惠总金额。</param>
    /// <param name="itemSubtotals">各 SKU 小计列表。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>各 SKU 分摊金额列表。</returns>
    Task<List<(Guid SkuId, decimal Allocation)>> CalculateAndAllocateAsync(decimal totalDiscount, List<(Guid SkuId, decimal Subtotal)> itemSubtotals, CancellationToken ct = default);
}
