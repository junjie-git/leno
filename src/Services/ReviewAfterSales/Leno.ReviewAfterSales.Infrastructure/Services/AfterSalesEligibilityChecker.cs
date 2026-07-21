using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.ReviewAfterSales.Infrastructure.Services;

/// <summary>
/// 售后资格校验器实现（M4 双轨方案重构后）。
/// 远程订单状态查询委托 <see cref="IOrderStatusProvider"/>（HttpClient 或 gRPC 双轨），本类仅保留业务规则校验与仓储查询。
/// 校验失败抛 <see cref="ReviewDomainException"/>。
/// </summary>
public sealed class AfterSalesEligibilityChecker : IAfterSalesEligibilityChecker
{
    private const int AfterSalesWindowDays = 15;
    private const int OrderStatusShipped = 2;
    private const int OrderStatusCompleted = 3;

    private readonly IOrderStatusProvider _orderStatusProvider;
    private readonly IAfterSalesRepository _afterSalesRepository;
    private readonly ILogger<AfterSalesEligibilityChecker> _logger;

    public AfterSalesEligibilityChecker(
        IOrderStatusProvider orderStatusProvider,
        IAfterSalesRepository afterSalesRepository,
        ILogger<AfterSalesEligibilityChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(orderStatusProvider);
        ArgumentNullException.ThrowIfNull(afterSalesRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _orderStatusProvider = orderStatusProvider;
        _afterSalesRepository = afterSalesRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrderStatusInfo> EnsureEligibleAsync(Guid orderId, Guid? orderLineId, Guid userId, AfterSalesType type, CancellationToken ct = default)
    {
        var order = await _orderStatusProvider.GetOrderStatusAsync(orderId, ct)
            .ConfigureAwait(false);

        if (order is null)
        {
            throw new ReviewDomainException("订单不存在或不可访问", "AFTERSALES_ORDER_NOT_FOUND");
        }

        if (order.UserId != userId)
        {
            throw new ReviewDomainException("无权操作此订单", "AFTERSALES_FORBIDDEN");
        }

        if (order.Status != OrderStatusShipped && order.Status != OrderStatusCompleted)
        {
            throw new ReviewDomainException("订单当前状态不支持售后申请", "AFTERSALES_STATUS_INVALID");
        }

        if (order.Status == OrderStatusCompleted
            && order.CompletedAt != default
            && DateTime.UtcNow - order.CompletedAt > TimeSpan.FromDays(AfterSalesWindowDays))
        {
            throw new ReviewDomainException("售后申请已超过期限", "AFTERSALES_WINDOW_EXPIRED");
        }

        if (orderLineId.HasValue)
        {
            var hasActive = await _afterSalesRepository.HasActiveByOrderLineAsync(orderLineId.Value, type, ct);
            if (hasActive)
            {
                throw new ReviewDomainException("该订单行已存在进行中的同类型售后单", "AFTERSALES_DUPLICATE");
            }
        }
        else
        {
            // 合并审计 3.3：整单售后（orderLineId 为 null）也需做重复申请校验，
            // 避免同订单重复提交整单售后造成重复退款。
            var hasActiveOrder = await _afterSalesRepository.HasActiveByOrderAsync(orderId, type, ct);
            if (hasActiveOrder)
            {
                throw new ReviewDomainException("该订单已存在进行中的同类型整单售后", "AFTERSALES_DUPLICATE");
            }
        }

        return order;
    }
}
