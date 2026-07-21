using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Services;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 积分按卖家分摊领域服务实现（P1-T19）。
/// 按各卖家小计占比分摊总积分抵现金额，使用 MidpointRounding.ToEven 与应用层原逻辑保持一致；
/// 尾差调整至最后一组以保证分摊之和精确等于 totalPointsOffset。
/// </summary>
public sealed class PointsAllocationService : IPointsAllocationService
{
    /// <inheritdoc />
    public IReadOnlyList<(Guid SellerId, decimal AllocatedPointsOffset)> AllocateBySellerRatio(
        IReadOnlyDictionary<Guid, decimal> sellerSubtotals,
        decimal totalPointsOffset)
    {
        ArgumentNullException.ThrowIfNull(sellerSubtotals);

        if (sellerSubtotals.Count == 0)
        {
            return Array.Empty<(Guid, decimal)>();
        }

        if (totalPointsOffset < 0)
        {
            throw new OrderDomainException("总积分抵现金额不可为负", "POINTS_OFFSET_NEGATIVE");
        }

        // 总积分为 0 时，各卖家分摊为 0
        if (totalPointsOffset == 0)
        {
            return sellerSubtotals.Select(kv => (kv.Key, 0m)).ToArray();
        }

        var sellers = sellerSubtotals.ToList();
        var sumSubtotals = sellers.Sum(kv => kv.Value);

        // 全部卖家金额为 0 时，全部归最后一组
        if (sumSubtotals == 0)
        {
            var result = sellers.Select(kv => (kv.Key, 0m)).ToList();
            result[^1] = (sellers[^1].Key, totalPointsOffset);
            return result;
        }

        var allocations = new List<(Guid SellerId, decimal AllocatedPointsOffset)>(sellers.Count);
        decimal allocated = 0;
        for (var i = 0; i < sellers.Count; i++)
        {
            var (sellerId, subtotal) = sellers[i];

            if (i == sellers.Count - 1)
            {
                // 最后一组吸收尾差，保证分摊之和等于 totalPointsOffset
                allocations.Add((sellerId, totalPointsOffset - allocated));
            }
            else
            {
                // 按小计占比分摊，零金额卖家分摊为 0
                var allocation = subtotal > 0
                    ? Math.Round(totalPointsOffset * (subtotal / sumSubtotals), 2, MidpointRounding.ToEven)
                    : 0m;
                allocations.Add((sellerId, allocation));
                allocated += allocation;
            }
        }

        return allocations;
    }
}
