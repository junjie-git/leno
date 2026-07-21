using Leno.Order.Application.Services;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Services;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 订单定价领域服务实现。
/// 价格校验防篡改：下单单价须与商品域当前售价一致；优惠分摊按小计比例分配，尾差调整至最后一项。
/// </summary>
public sealed class OrderPricingDomainService : IOrderPricingDomainService
{
    private readonly IProductAntiCorruptionService _productAntiCorruption;

    public OrderPricingDomainService(IProductAntiCorruptionService productAntiCorruption)
    {
        _productAntiCorruption = productAntiCorruption;
    }

    /// <inheritdoc />
    public Task ValidatePricesAsync(List<(Guid SkuId, decimal ExpectedPrice)> skuPrices, IReadOnlyDictionary<Guid, decimal> skuCurrentPrices, CancellationToken ct = default)
    {
        foreach (var (skuId, expectedPrice) in skuPrices)
        {
            if (!skuCurrentPrices.TryGetValue(skuId, out var currentPrice))
            {
                throw new OrderDomainException($"SKU {skuId} 不存在或已下架", "ORDER_SKU_NOT_FOUND");
            }

            if (currentPrice != expectedPrice)
            {
                throw new OrderDomainException("商品价格已变更，请重新下单", "ORDER_PRICE_CHANGED");
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<(Guid SkuId, decimal Allocation)>> CalculateAndAllocateAsync(
        decimal totalDiscount,
        List<(Guid SkuId, decimal Subtotal)> itemSubtotals,
        CancellationToken ct = default)
    {
        var result = new List<(Guid SkuId, decimal Allocation)>();

        if (itemSubtotals.Count == 0)
        {
            return Task.FromResult(result);
        }

        // 无优惠或小计之和为 0 时，各 SKU 分摊为 0
        var sumSubtotals = itemSubtotals.Sum(x => x.Subtotal);
        if (totalDiscount == 0 || sumSubtotals == 0)
        {
            foreach (var (skuId, _) in itemSubtotals)
            {
                result.Add((skuId, 0m));
            }

            return Task.FromResult(result);
        }

        decimal allocated = 0;
        for (var i = 0; i < itemSubtotals.Count; i++)
        {
            var (skuId, subtotal) = itemSubtotals[i];

            if (i == itemSubtotals.Count - 1)
            {
                // 最后一项吸收尾差，保证分摊之和等于 totalDiscount
                result.Add((skuId, totalDiscount - allocated));
            }
            else
            {
                var allocation = Math.Round(totalDiscount * subtotal / sumSubtotals, 2);
                result.Add((skuId, allocation));
                allocated += allocation;
            }
        }

        return Task.FromResult(result);
    }
}
