using Leno.Review.Domain.Exceptions;
using Leno.Review.Domain.Repositories;
using Leno.Review.Domain.Services;
using Leno.SharedContracts.Enums;
using Microsoft.Extensions.Logging;

namespace Leno.Review.Infrastructure.Services;

/// <summary>
/// 评价资格校验器实现（评价 BC 独立维护，M4 双轨方案重构后）。
/// 远程订单状态查询委托 <see cref="IOrderStatusProvider"/>（HttpClient 或 gRPC 双轨），本类仅保留业务规则校验与仓储查询。
/// 校验失败抛 <see cref="ReviewDomainException"/>。
/// 订单状态码引用共享枚举 <see cref="OrderStatusEnum"/>（审计 3.9），避免魔法数跨 BC 契约脆弱。
/// </summary>
public sealed class ReviewEligibilityChecker : IReviewEligibilityChecker
{
    private const int ReviewWindowDays = 30;

    private readonly IOrderStatusProvider _orderStatusProvider;
    private readonly IReviewRepository _reviewRepository;
    private readonly ILogger<ReviewEligibilityChecker> _logger;

    public ReviewEligibilityChecker(
        IOrderStatusProvider orderStatusProvider,
        IReviewRepository reviewRepository,
        ILogger<ReviewEligibilityChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(orderStatusProvider);
        ArgumentNullException.ThrowIfNull(reviewRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _orderStatusProvider = orderStatusProvider;
        _reviewRepository = reviewRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrderItemStatusInfo> EnsureEligibleAsync(Guid orderId, Guid orderLineId, Guid userId, CancellationToken ct = default)
    {
        var order = await _orderStatusProvider.GetOrderStatusAsync(orderId, ct)
            .ConfigureAwait(false);

        if (order is null)
        {
            throw new ReviewDomainException("订单不存在或不可访问", "REVIEW_ORDER_NOT_FOUND");
        }

        if (order.UserId != userId)
        {
            throw new ReviewDomainException("无权操作此订单", "REVIEW_FORBIDDEN");
        }

        if (order.Status != (int)OrderStatusEnum.Completed)
        {
            throw new ReviewDomainException("订单未完成，不可评价", "REVIEW_ORDER_NOT_COMPLETED");
        }

        if (order.CompletedAt != default
            && DateTime.UtcNow - order.CompletedAt > TimeSpan.FromDays(ReviewWindowDays))
        {
            throw new ReviewDomainException("评价已超过期限", "REVIEW_WINDOW_EXPIRED");
        }

        var lineItem = order.Items.FirstOrDefault(i => i.OrderLineId == orderLineId);
        if (lineItem is null)
        {
            throw new ReviewDomainException("订单行不存在", "REVIEW_ORDER_LINE_NOT_FOUND");
        }

        var exists = await _reviewRepository.ExistsByOrderLineAsync(orderLineId, ct);
        if (exists)
        {
            throw new ReviewDomainException("该订单行已评价", "REVIEW_DUPLICATE");
        }

        return lineItem;
    }
}
