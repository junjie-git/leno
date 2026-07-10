using Leno.ReviewAfterSales.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.ReviewAfterSales.Infrastructure.Services;

/// <summary>
/// 评价资格校验器防腐层实现（占位）。
/// 实际实现应通过 HTTP 调用订单域 API 校验订单已完成、订单行未重复评价、在评价期限内且申请人为订单买家。
/// 当前为占位桩，始终判定为可评价（不抛异常），仅记录警告日志。
/// </summary>
public sealed class ReviewEligibilityChecker : IReviewEligibilityChecker
{
    private readonly ILogger<ReviewEligibilityChecker> _logger;

    public ReviewEligibilityChecker(ILogger<ReviewEligibilityChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task EnsureEligibleAsync(Guid orderId, Guid orderLineId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogWarning("评价资格校验为占位桩实现，默认放行 OrderId={OrderId} OrderLineId={OrderLineId} UserId={UserId}",
            orderId, orderLineId, userId);

        return Task.CompletedTask;
    }
}
