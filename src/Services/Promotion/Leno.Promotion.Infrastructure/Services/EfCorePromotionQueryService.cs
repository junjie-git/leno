using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Promotion.Infrastructure.Services;

/// <summary>
/// 促销防腐层实现，只读查询当前 Active 且在有效时间区间内的满减活动，试算优惠。
/// 供订单域等下游上下文查询适用优惠，不暴露促销域领域模型。
/// </summary>
public sealed class EfCorePromotionQueryService : IPromotionQueryService
{
    private readonly IPromotionActivityRepository _activityRepository;

    public EfCorePromotionQueryService(IPromotionActivityRepository activityRepository)
    {
        ArgumentNullException.ThrowIfNull(activityRepository);
        _activityRepository = activityRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApplicablePromotionSnapshot>> GetApplicablePromotionsAsync(
        decimal orderAmount,
        Guid? sellerId,
        CancellationToken ct = default)
    {
        if (orderAmount <= 0)
        {
            return Array.Empty<ApplicablePromotionSnapshot>();
        }

        var now = DateTime.UtcNow;
        var activities = await _activityRepository.GetActiveAsync(now, ct);

        var snapshots = new List<ApplicablePromotionSnapshot>();
        foreach (var activity in activities)
        {
            var discount = activity.CalculateDiscount(orderAmount);
            if (discount > 0)
            {
                var matchedRule = activity.Rules.LastOrDefault(r => orderAmount >= r.ThresholdAmount);
                snapshots.Add(new ApplicablePromotionSnapshot
                {
                    ActivityId = activity.Id,
                    Name = activity.Name,
                    Type = activity.Type,
                    DiscountAmount = discount,
                    ThresholdAmount = matchedRule?.ThresholdAmount ?? 0,
                    EndTime = activity.EndTime
                });
            }
        }

        return snapshots;
    }
}
